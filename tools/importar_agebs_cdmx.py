import json
import sys
import pyodbc
from datetime import datetime

SERVER = r"LAPTOP-6VL1O8NQ\SQLEXPRESS"
DATABASE = "SNCEV"

CONNECTION_STRING = (
    "DRIVER={ODBC Driver 18 for SQL Server};"
    f"SERVER={SERVER};"
    f"DATABASE={DATABASE};"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)

FUENTE = "AgebsCDMX.json"


def validar_feature(feature, numero):
    props = feature.get("properties") or {}
    geometry = feature.get("geometry") or {}

    requeridas = [
        "CVEGEO",
        "CVE_ENT",
        "CVE_MUN",
        "CVE_LOC",
        "CVE_AGEB",
    ]

    faltantes = [
        x for x in requeridas
        if not props.get(x)
    ]

    if faltantes:
        raise ValueError(
            f"Feature {numero}: faltan propiedades "
            f"{', '.join(faltantes)}"
        )

    if geometry.get("type") not in ("Polygon", "MultiPolygon"):
        raise ValueError(
            f"Feature {numero}: geometría no soportada: "
            f"{geometry.get('type')}"
        )

    return props


def main(ruta):
    print("=" * 70)
    print("SNCEV - IMPORTADOR DE AGEB CDMX")
    print("=" * 70)

    with open(ruta, "r", encoding="utf-8") as archivo:
        geojson = json.load(archivo)

    if geojson.get("type") != "FeatureCollection":
        raise ValueError(
            "El archivo no es un GeoJSON FeatureCollection."
        )

    features = geojson.get("features", [])

    print(f"Archivo     : {ruta}")
    print(f"Features    : {len(features):,}")

    cvegeo_vistas = set()

    registros = []

    for numero, feature in enumerate(features, start=1):

        props = validar_feature(feature, numero)

        cvegeo = str(props["CVEGEO"]).strip()
        cve_ent = str(props["CVE_ENT"]).strip()
        cve_mun = str(props["CVE_MUN"]).strip()
        cve_loc = str(props["CVE_LOC"]).strip()
        cve_ageb = str(props["CVE_AGEB"]).strip()

        if cvegeo in cvegeo_vistas:
            raise ValueError(
                f"CVEGEO duplicada: {cvegeo}"
            )

        cvegeo_vistas.add(cvegeo)

        # Validaciones estructurales
        if len(cve_ent) != 2:
            raise ValueError(
                f"CVE_ENT inválida: {cve_ent}"
            )

        if len(cve_mun) != 3:
            raise ValueError(
                f"CVE_MUN inválida: {cve_mun}"
            )

        if len(cve_loc) != 4:
            raise ValueError(
                f"CVE_LOC inválida: {cve_loc}"
            )

        if len(cve_ageb) != 4:
            raise ValueError(
                f"CVE_AGEB inválida: {cve_ageb}"
            )

        if len(cvegeo) != 13:
            raise ValueError(
                f"CVEGEO inválida: {cvegeo}"
            )

        # Para este importador esperamos CDMX.
        if cve_ent != "09":
            raise ValueError(
                f"Se encontró una entidad distinta de CDMX: "
                f"{cve_ent}"
            )

        registros.append(
            (
                cvegeo,
                cve_ent,
                cve_mun,
                cve_loc,
                cve_ageb,
            )
        )

    print(f"CVEGEO únicas: {len(cvegeo_vistas):,}")
    print("Validación GeoJSON: OK")
    print()

    conn = pyodbc.connect(
        CONNECTION_STRING,
        autocommit=False
    )

    cursor = conn.cursor()

    insertados = 0
    actualizados = 0

    try:

        for (
            cvegeo,
            cve_ent,
            cve_mun,
            cve_loc,
            cve_ageb
        ) in registros:

            # --------------------------------------------
            # Obtener municipio SNCEV
            # clave_inegi = entidad + municipio
            # Ejemplo: 09 + 010 = 09010
            # --------------------------------------------

            clave_municipio_inegi = (
                cve_ent + cve_mun
            )

            cursor.execute(
                """
                SELECT id
                FROM sncev_geo.municipio
                WHERE clave_inegi = ?
                  AND activo = 1
                """,
                clave_municipio_inegi,
            )

            municipio = cursor.fetchone()

            if municipio is None:
                raise ValueError(
                    "No existe municipio SNCEV para "
                    f"{clave_municipio_inegi} "
                    f"(CVEGEO {cvegeo})"
                )

            municipio_id = municipio[0]

            # --------------------------------------------
            # ¿Ya existe?
            # --------------------------------------------

            cursor.execute(
                """
                SELECT clave
                FROM sncev_geo.ageb
                WHERE clave = ?
                """,
                cvegeo,
            )

            existente = cursor.fetchone()

            if existente:

                cursor.execute(
                    """
                    UPDATE sncev_geo.ageb
                    SET
                        municipio_id = ?,
                        estado_clave = ?,
                        cve_ent = ?,
                        cve_mun = ?,
                        cve_loc = ?,
                        cve_ageb = ?,
                        cvegeo = ?,
                        tiene_geometria = 1,
                        fuente_geografica = ?,
                        fecha_actualizacion_geo =
                            SYSDATETIME()
                    WHERE clave = ?
                    """,
                    municipio_id,
                    cve_ent,
                    cve_ent,
                    cve_mun,
                    cve_loc,
                    cve_ageb,
                    cvegeo,
                    FUENTE,
                    cvegeo,
                )

                actualizados += 1

            else:

                cursor.execute(
                    """
                    INSERT INTO sncev_geo.ageb
                    (
                        clave,
                        nombre,
                        tipo,
                        municipio_id,
                        estado_clave,
                        cve_ent,
                        cve_mun,
                        cve_loc,
                        cve_ageb,
                        cvegeo,
                        tiene_geometria,
                        fuente_geografica,
                        fecha_actualizacion_geo
                    )
                    VALUES
                    (
                        ?, ?, ?, ?, ?,
                        ?, ?, ?, ?, ?,
                        1, ?, SYSDATETIME()
                    )
                    """,
                    cvegeo,

                    # Nombre descriptivo provisional.
                    f"AGEB {cve_ageb}",

                    # U = urbana.
                    "U",

                    municipio_id,
                    cve_ent,
                    cve_ent,
                    cve_mun,
                    cve_loc,
                    cve_ageb,
                    cvegeo,
                    FUENTE,
                )

                insertados += 1

        conn.commit()

        print("=" * 70)
        print("IMPORTACIÓN COMPLETADA")
        print("=" * 70)
        print(f"AGEB procesadas : {len(registros):,}")
        print(f"Insertadas      : {insertados:,}")
        print(f"Actualizadas    : {actualizados:,}")
        print("=" * 70)

    except Exception:
        conn.rollback()
        raise

    finally:
        cursor.close()
        conn.close()


if __name__ == "__main__":

    if len(sys.argv) != 2:
        print(
            "Uso: py importar_agebs_cdmx.py "
            "AgebsCDMX.json"
        )
        sys.exit(1)

    main(sys.argv[1])