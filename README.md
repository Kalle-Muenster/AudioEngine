# AudioEngine
Mix and Synthetize audio data - by parsing tone scripts,... independendantly from either data type nor channel constellation!*

(*)
- Supported data types:
-----------------------
-- 8bit, 16bit, 24bit signed integer data
-- 32bit, 64bit floating point data

- Supported channel cnstellations:
----------------------------------
-- mono
-- stereo
-- quadro
-- 5.1
-- 7.1

- Supported file formats
------------------------
-- Windows wave forms (wav)
-- Sun audio files (au, snd)
-- NetPbm tracks (pam)

## What it is not: a compression library
- So not supported are:
-- Any stream types which using lossy data compression,.. like mp3, atrac, mpa, aac, ac3 ...
-- Just PCM formats which are transporting plain, uncompressed pcm data are supported yet.
