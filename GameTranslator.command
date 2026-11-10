#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
if [ -d "/Applications/GameTranslator.app" ]; then
    open "/Applications/GameTranslator.app"
elif [ -d "$DIR/dist/GameTranslator.app" ]; then
    open "$DIR/dist/GameTranslator.app"
else
    echo "GameTranslator.app bulunamadı. Lütfen mac/ klasöründeki build.sh ile derleyin."
fi