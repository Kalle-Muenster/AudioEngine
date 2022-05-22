AudioEngine
===========
Mix and Synthetize audio data - by parsing tone scripts,... independently from (supported) data types and channel constellations


### Supported data types:

- 8bit, 16bit, 24bit signed integer data
- 32bit, 64bit floating point data

### Supported channel cnstellations:

- mono
- stereo
- quadro
- 5.1
- 7.1

### Supported file formats

- Windows wave form files (wav)
- Sun audio files (au, snd)
- NetPbm files (pam) ... (for transporting audiodata quite uncommon.. but really handy format for this)

## Not supported: 
- Anything which is using lossy data compression like: mp3, atrac, mpa, aac, ac3 ... these never will be supported by this library at all. this is not a compression library!
- Anything which is using lossles data compression,.. like flc, ape, other lossess formats, maybe will gain support later some days in the future.  

