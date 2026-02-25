#!/bin/bash
trap 'kill 0' EXIT

dotnet run --project spaced-md.Server &
cd spaced-md.client && npm run dev &

wait
