AudioEngine
===========
Mix and Synthetize audio data - by parsing tone scripts,... independendantly from data type and channel constellation!


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

- Windows wave forms (wav)
- Sun audio files (au, snd)
- NetPbm tracks (pam)

## Not supported is: 
- Anything which is using lossy data compression,.. like mp3, atrac, mpa, aac, ac3 ... (will never be supported by this library at all, e.g. this AudioEngine is not a compression library!)
- Anything which is using lossles data compression,.. like flc, ape, other lossess formats (maybe will gain support later some days in the future)  

