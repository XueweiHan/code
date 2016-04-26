SRT Tools

Merge 2 srt files
SrtUtility -m <file1> <file2> <output>

Smart merge 2 srt files -- automatic find the best merge result, shift all times in file1 or file2 to match another file then merge, best means the merge result is the smallest.
SrtUtility -sm <file1> <file2> <0|1> <output>

Time shift -- shift all time in the srt with a time delta, which is calculate from the 2nd time minus 1st time.
SrtUtility -t <input> <hh:mm:ss,fff> <hh:mm:ss,fff> <output>
