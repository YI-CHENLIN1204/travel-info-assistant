# Airport catalogue

`airports.min.json` is a compact snapshot of airports with scheduled service and an IATA code
from the public-domain [OurAirports data set](https://ourairports.com/data/). It is used only for
local autocomplete and airport validation, so typing in the flight form never consumes an
AeroDataBox API unit.

Regenerate it from an upstream `airports.csv` file with:

```bash
python tools/build-airport-catalog.py airports.csv \
  backend/src/TravelInfoAssistant.Api/Data/airports.min.json \
  --source-commit <ourairports-data-commit>
```
