#!/bin/sh
# Thumbnailer for OpenRA maps
#
# Parameters :
#   $1 - URI of map file
#   $2 - full path of generated thumbnail
#   $3 - height of thumbnail in pixels

# check tools availability
command -v gvfs-copy >/dev/null 2>&1 || exit 1
command -v unzip >/dev/null 2>&1 || exit 1
command -v convert >/dev/null 2>&1 || exit 1

# get parameters
FILE_URI=$1
FILE_THUMB=$2
HEIGHT=$3

# get filename extension (can be .zip or .oramap)
FILE_EXT=$(echo "$FILE_URI" | sed 's/^.*\.\(.*\)/\1/')

# generate temporary local filename
TMP_FILE=$(mktemp -t XXXXXXXX.${FILE_EXT})

# copy input file (from possibly foreign file system) to temporary local file
gvfs-copy "${FILE_URI}" "${TMP_FILE}"

# extract embedded map preview
unzip "${TMP_FILE}" map.png -d "${FILE_THUMB}"

# generate thumbnail using imagemagick
convert "${FILE_THUMB}/map.png" -resize x${HEIGHT} "${FILE_THUMB}/map.png"

# remove temporary local file
rm ${TMP_FILE}
