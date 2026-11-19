import base64
import json
import sys
from datetime import datetime

from dissect.esedb import EseDB


def encode(value):
    if value is None:
        return None
    if isinstance(value, bytes):
        return {"base64": base64.b64encode(value).decode("ascii")}
    if isinstance(value, bytearray):
        return {"base64": base64.b64encode(bytes(value)).decode("ascii")}
    if isinstance(value, bool):
        return {"bool": value}
    if isinstance(value, int):
        return {"int": value}
    if isinstance(value, float):
        return {"float": value}
    if isinstance(value, datetime):
        return {"datetime": value.isoformat()}
    return {"string": str(value)}


if len(sys.argv) != 4:
    raise SystemExit("usage: esedb-wrapper.py rows|columns DATABASE TABLE")

with open(sys.argv[2], "rb") as stream:
    database = EseDB(stream)
    table = database.table(sys.argv[3])

    if sys.argv[1] == "columns":
        print(json.dumps([column.name for column in table.columns.values()]))
    else:
        print(json.dumps([
            {name: encode(value) for name, value in record.as_dict().items()}
            for record in table.records()
        ]))
