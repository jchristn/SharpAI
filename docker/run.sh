if [ -z "${IMG_TAG}" ]; then
  IMG_TAG='v1.0.0'
fi

echo Using image tag $IMG_TAG

if [ ! -f "sharpai.json" ]
then
  echo Configuration file sharpai.json not found.
  exit
fi

# Items that require persistence
#   sharpai.json
#   sharpai.db
#   logs/
#   models/

# Argument order matters!

docker run \
  -p 8000:8000 \
  -t \
  -i \
  -e "TERM=xterm-256color" \
  -v ./sharpai.json:/app/sharpai.json \
  -v ./sharpai.db:/app/sharpai.db \
  -v ./logs/:/app/logs/ \
  -v ./models/:/app/models/ \
  jchristn77/sharpai:$IMG_TAG

