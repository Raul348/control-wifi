"""
Convierte la base de datos de perfiles AISC (Excel "AISCProp13-1.0.xlsx") en el archivo
perfiles_aisc.csv que se incrusta en PerfilesAisc.Core.

Uso:  python convertir_aisc.py  AISCProp13-1.0.xlsx  ../src/PerfilesAisc.Core/Datos/perfiles_aisc.csv

Los valores se guardan tal como vienen en el Excel (pulgadas, pulgadas², libras por pie...).
La conversión a milímetros la hace el programa.
"""
import csv
import sys

import openpyxl

# Columna del CSV -> nombres posibles del encabezado en el Excel (cambian según la hoja).
COLUMNAS = {
    "A": ["A"],
    "d": ["d"],
    "bf": ["bf"],
    "tw": ["tw"],
    "tf": ["tf"],
    "k": ["k(des)", "k"],
    "k1": ["k1"],
    "T": ["T"],
    "gage": ["gage"],
    "wt": ["wt./ft."],
    "b": ["b"],
    "t": ["t"],
    "tnom": ["t(nom)"],
    "tdes": ["t(des)"],
    "h": ["h"],
    "OD": ["OD"],
    "ID": ["ID"],
    "Ix": ["Ix"],
    "Sx": ["Sx"],
    "rx": ["rx"],
    "Zx": ["Zx"],
    "Iy": ["Iy"],
    "Sy": ["Sy"],
    "ry": ["ry"],
    "Zy": ["Zy"],
    "rz": ["rz"],
    "J": ["J"],
    "Cw": ["Cw"],
    "xbar": ["x(bar)"],
    "ybar": ["y(bar)"],
    "eo": ["eo"],
    "Asurf": ["A(surf)"],
}

# Hoja del Excel -> grupo del catálogo.
GRUPOS = {
    "W, M, S & HP": "I",
    "C & MC": "C",
    "WT, MT, & ST": "T",
    "Single Angles": "L",
    "Double Angles": "2L",
    "Rect.  & Square HSS": "HSSR",
    "Round HSS & Pipes": "HSSO",
}


def main(entrada, salida):
    wb = openpyxl.load_workbook(entrada, read_only=True, data_only=True)
    filas = []
    for ws in wb.worksheets:
        grupo = GRUPOS.get(ws.title)
        if grupo is None:
            print("Hoja ignorada:", ws.title)
            continue
        hojas = list(ws.iter_rows(values_only=True))
        encabezado = [str(c).strip() if c is not None else "" for c in hojas[0]]
        indice = {}
        for col, nombres in COLUMNAS.items():
            for n in nombres:
                if n in encabezado:
                    indice[col] = encabezado.index(n)
                    break
        n = 0
        for r in hojas[1:]:
            if not r or not r[0] or not str(r[0]).strip():
                continue
            fila = {"Grupo": grupo, "Nombre": str(r[0]).strip()}
            for col, i in indice.items():
                v = r[i] if i < len(r) else None
                if isinstance(v, (int, float)):
                    fila[col] = f"{v:.6g}"
                elif v not in (None, "", "-", "–"):
                    try:
                        fila[col] = f"{float(v):.6g}"
                    except ValueError:
                        pass
            filas.append(fila)
            n += 1
        print(f"{ws.title}: {n} perfiles, columnas {sorted(indice)}")

    campos = ["Grupo", "Nombre"] + list(COLUMNAS)
    with open(salida, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=campos, lineterminator="\n")
        w.writeheader()
        w.writerows(filas)
    print("Total:", len(filas), "->", salida)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
