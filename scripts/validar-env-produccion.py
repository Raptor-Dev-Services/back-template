#!/usr/bin/env python3
"""Valida la hoja de secretos de produccion ANTES de subirla a GitHub Secrets / Variables.

Dos pasadas:

  1. ESTATICA  forma de cada valor sin salir a la red: que esten todas las claves que consumen los
               workflows, sin comentarios pegados ni espacios sobrantes, claves con largo minimo, llaves
               distintas entre si, URLs https y sin localhost.
  2. EN VIVO   cada credencial contra SU servicio, SOLO LECTURA: se conecta, pregunta y se va. Ni un
               INSERT ni un correo enviado.

REGLA DE ORO: **nunca imprime un valor**. Solo el nombre de la clave, su largo y el veredicto; la salida
se puede pegar en un ticket sin revisar nada.

Por que existe: "conecta" no es "sirve". Una cadena de Postgres que conecta como un rol con BYPASSRLS
pasa un `SELECT 1` y luego la API no arranca (RlsRoleGuard). Hay que preguntar por el rol, que es lo que
mira la guarda de arranque.

Uso:
    python3 scripts/validar-env-produccion.py .env.production              # estatica + en vivo
    python3 scripts/validar-env-produccion.py .env.production --estatica   # sin salir a la red

Solo biblioteca estandar. La sonda de Postgres usa Docker con postgres:17-alpine (psql en un contenedor
desechable, credenciales por entorno y nunca en la linea de comandos).

Sale con codigo 1 si hay alguna FALLA.
"""

from __future__ import annotations

import argparse
import os
import pathlib
import re
import smtplib
import ssl
import subprocess
import sys

# Lo que consumen .github/workflows/*.yml como SECRETS. GITHUB_TOKEN lo pone GitHub.
SECRETOS = [
    "ConnectionStrings__DefaultConnection",
    "Jwt__Key",
    "Totp__EncryptionKey",
    "Smtp__Password",
    "PG_OWNER_URI",
    "PG_OWNER_PASSWORD",
    "VPS_HOST",
    "VPS_USER",
    "VPS_SSH_KEY",
]

# Opcionales: si estan, se validan; si faltan, es una decision (p. ej. bootstrap apagado).
OPCIONALES = ["Bootstrap__Secret"]

# Config NO secreta que el deploy necesita (GitHub VARIABLES).
VARIABLES = ["Web__BaseUrl", "Cors__AllowedOrigins", "Smtp__Host", "Smtp__From", "API_DOMAIN", "IMAGE_NAME"]

MINIMO_BYTES = {"Jwt__Key": 32, "Totp__EncryptionKey": 32, "Bootstrap__Secret": 32}

fallas: list[str] = []
avisos: list[str] = []
verdes: list[str] = []


def falla(msg: str) -> None:
    fallas.append(msg)
    print(f"  [FALLA] {msg}")


def aviso(msg: str) -> None:
    avisos.append(msg)
    print(f"  [AVISO] {msg}")


def verde(msg: str) -> None:
    verdes.append(msg)
    print(f"  [OK]    {msg}")


def leer(ruta: str) -> dict[str, str]:
    d: dict[str, str] = {}
    for linea in pathlib.Path(ruta).read_text(encoding="utf-8").splitlines():
        s = linea.strip()
        if not s or s.startswith("#") or "=" not in s:
            continue
        k, v = s.split("=", 1)
        k, v = k.strip(), v.strip()
        if len(v) > 1 and v[0] in "\"'" and v[-1] == v[0]:
            v = v[1:-1]
        d[k] = v
    return d


def estatica(d: dict[str, str]) -> None:
    print("\n== ESTATICA ==\n")

    for k in SECRETOS + VARIABLES:
        if k not in d:
            falla(f"{k}: no esta en la hoja, y el deploy la consume")
        elif not d[k]:
            falla(f"{k}: esta vacia")

    for k in OPCIONALES:
        if not d.get(k):
            aviso(f"{k}: vacia; el endpoint correspondiente queda APAGADO (puede ser lo que quieres)")

    for k, v in d.items():
        if re.search(r"\s+#", v):
            falla(f"{k}: comentario pegado al valor; GitHub guardaria el comentario como parte del secreto")
        if v != v.strip() or "\t" in v:
            falla(f"{k}: espacios o tabuladores sobrantes")

    for k, minimo in MINIMO_BYTES.items():
        v = d.get(k, "")
        if not v:
            continue
        largo = len(v.encode("utf-8"))
        (verde if largo >= minimo else falla)(f"{k}: {largo} bytes" + ("" if largo >= minimo else f", por debajo de {minimo}"))
        if "change" in v.lower() and "me" in v.lower():
            falla(f"{k}: parece un placeholder de ejemplo")

    llaves = [d.get(k) for k in ("Jwt__Key", "Totp__EncryptionKey", "Bootstrap__Secret") if d.get(k)]
    if len(llaves) != len(set(llaves)):
        falla("Jwt__Key, Totp__EncryptionKey y Bootstrap__Secret deben ser DISTINTAS entre si")

    for k in ("Web__BaseUrl",):
        v = d.get(k, "")
        if v and not v.startswith("https://"):
            falla(f"{k}: no es https")
        if "localhost" in v or "127.0.0.1" in v:
            falla(f"{k}: apunta a localhost")

    for origen in [o.strip() for o in d.get("Cors__AllowedOrigins", "").split(",") if o.strip()]:
        if origen == "*" or not origen.startswith("https://"):
            falla(f"Cors__AllowedOrigins: '{len(origen)} caracteres' no es un origen https explicito")

    cadena = d.get("ConnectionStrings__DefaultConnection", "")
    if cadena and re.search(r"(?i)username=(postgres|[a-z0-9_]*_owner)\b", cadena):
        falla("ConnectionStrings__DefaultConnection: usa el rol dueno o postgres; la API debe conectarse con el rol _app")

    llave = d.get("VPS_SSH_KEY", "")
    if llave and not ("BEGIN" in llave and "PRIVATE KEY" in llave):
        falla(f"VPS_SSH_KEY: no parece una llave privada ({len(llave)} caracteres, sin cabecera PEM)")

    if d.get("ASPNETCORE_ENVIRONMENT", "Production").lower() != "production":
        falla("ASPNETCORE_ENVIRONMENT no dice Production")
    if d.get("AUTO_DEPLOY", "false").lower() not in ("true", "false"):
        falla("AUTO_DEPLOY: el valor no es true ni false; el job de despliegue se saltaria sin error")


def pg_env(cadena: str) -> dict[str, str]:
    kv = {}
    for parte in cadena.split(";"):
        if "=" in parte:
            a, b = parte.split("=", 1)
            kv[a.strip().lower()] = b.strip()
    return {
        "PGHOST": kv.get("host", ""),
        "PGPORT": kv.get("port", "5432"),
        "PGDATABASE": kv.get("database", ""),
        "PGUSER": kv.get("username") or kv.get("user id", ""),
        "PGPASSWORD": kv.get("password", ""),
        "PGSSLMODE": (kv.get("ssl mode") or kv.get("sslmode") or "require").lower().replace(" ", ""),
    }


def psql(env: dict[str, str], sql: str) -> tuple[int, str]:
    cmd = ["docker", "run", "--rm", "-i"]
    for k in env:
        cmd += ["-e", k]
    cmd += ["postgres:17-alpine", "psql", "-tAc", sql]
    r = subprocess.run(cmd, capture_output=True, text=True, env={**os.environ, **env})
    return r.returncode, (r.stdout or r.stderr).strip()


def probar_postgres(d: dict[str, str]) -> None:
    print("\n-- Postgres (la cadena de la APLICACION)")
    env = pg_env(d.get("ConnectionStrings__DefaultConnection", ""))
    if not env["PGHOST"]:
        falla("ConnectionStrings__DefaultConnection: no se pudo interpretar el host")
        return

    code, out = psql(env, "SELECT current_user||'|'||rolsuper::text||'|'||rolbypassrls::text FROM pg_roles WHERE rolname=current_user;")
    if code != 0:
        falla("Postgres (app): no conecta")
        return

    rol, superusuario, bypass = (out.split("|") + ["?"] * 3)[:3]
    verde(f"Postgres (app): conecta como un rol de {len(rol)} caracteres")
    if superusuario == "true" or bypass == "true":
        falla("Postgres (app): el rol es superusuario o tiene BYPASSRLS; RlsRoleGuard no dejara arrancar la API")
    else:
        verde("Postgres (app): sin superusuario ni BYPASSRLS, como exige la guarda de arranque")

    code, out = psql(env, "SELECT count(*) FROM pg_policies;")
    if code == 0:
        (verde if out.strip() != "0" else aviso)(f"Postgres (app): {out.strip()} policies de RLS (el script de RLS es un paso del deploy)")


def probar_smtp(d: dict[str, str]) -> None:
    print("\n-- SMTP (autentica y cuelga; no manda nada)")
    host = d.get("Smtp__Host", "")
    puerto = int(d.get("Smtp__Port", "587") or 587)
    try:
        with smtplib.SMTP(host, puerto, timeout=20) as s:
            s.starttls(context=ssl.create_default_context())
            if d.get("Smtp__User"):
                s.login(d.get("Smtp__User", ""), d.get("Smtp__Password", ""))
        verde(f"SMTP: conexion y AUTH correctos en el puerto {puerto}")
    except smtplib.SMTPAuthenticationError:
        falla("SMTP: el servidor rechazo las credenciales")
    except Exception as e:  # noqa: BLE001 - se reporta el tipo, nunca el mensaje (puede llevar datos)
        aviso(f"SMTP: no se pudo comprobar ({type(e).__name__})")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("archivo")
    ap.add_argument("--estatica", action="store_true", help="no sale a la red")
    args = ap.parse_args()

    d = leer(args.archivo)
    print(f"Hoja: {args.archivo}   claves: {len(d)}   (este script NUNCA imprime valores)")
    estatica(d)

    if not args.estatica:
        print("\n== EN VIVO (solo lectura) ==")
        probar_postgres(d)
        probar_smtp(d)

    print(f"\n{'=' * 70}\nRESULTADO: {'ROJO' if fallas else 'VERDE'}   fallas={len(fallas)} avisos={len(avisos)} en verde={len(verdes)}\n{'=' * 70}")
    return 1 if fallas else 0


if __name__ == "__main__":
    sys.exit(main())
