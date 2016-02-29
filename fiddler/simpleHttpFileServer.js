    static function OnBeforeRequest(oSession: Session) {

        oSession.oRequest['x-pid'] = oSession.LocalProcess;

        // simple http file server
        if (oSession.HostnameIs(System.Environment.MachineName) || oSession.HostnameIs('localhost') || oSession.HostnameIs('f.cn')) {
            try {
                var path = decodeURIComponent(oSession.PathAndQuery).substring(1);//.Insert(1, ":");
                oSession.oRequest['x-filepath'] = path;
                var found = true;

                if (System.IO.File.Exists(path)) {
                    
                    var fileExt = path.split('.').pop();
                    var contentType = { 'cmd': 'text/plain', 'log': 'text/plain' }[fileExt];

                    oSession.utilCreateResponseAndBypassServer();
                    oSession.LoadResponseFromFile(path);
                    if (contentType) {
                        oSession.oResponse['Content-Type'] = contentType;
                    }
                    contentType = oSession.oResponse['Content-Type'];
                    if (contentType.StartsWith('text') || contentType.StartsWith('application/json')) {
                        oSession.utilGZIPResponse();
                        oSession.oResponse['Content-Encoding'] = 'gzip';
                    }
                } else if (System.IO.Directory.Exists(path)) {
                    if (!path.EndsWith('/')) {
                        oSession.utilCreateResponseAndBypassServer();
                        oSession.responseCode = 307;
                        oSession.oResponse['Location'] = '/' + path + '/';
                    } else if (oSession.RequestMethod === 'POST') {
                        var bytesMatch = function(s: Byte[], p: Byte[], i: int) {
                                var sLen = s.Length;
                                var pLen = p.Length;
                                var i = i || 0;
                                var j = 0;
                                while (i < sLen && j < pLen) {
                                    if (s[i] === p[j]) {
                                        i++;
                                        j++;
                                    } else {
                                        i -= j - 1;
                                        j = 0;
                                    }
                                }
                                return j === pLen ? i - j : -1;
                            };
                            
                        var type = oSession.oRequest['Content-Type'];
                        var m = type && type.match(/boundary=(.*)$/);
                        var boundary = m && m[1];
                        if (boundary) {
                            var boundaryBytes = System.Text.Encoding.ASCII.GetBytes('\r\n--' + boundary);
                            var newLineBytes = System.Text.Encoding.ASCII.GetBytes('\r\n\r\n');
                            oSession.utilDecodeRequest();
                            var body = oSession.RequestBody;
                            var index = 0;
                            while (index >= 0) {
                                var headEnd = bytesMatch(body, newLineBytes, index);
                                if (headEnd < 0) break; else headEnd += 4;
                                var head = System.Text.Encoding.UTF8.GetString(body, index, headEnd - index);
                                var fileName = System.IO.Path.Combine(path, head.match(/filename="(.*?)"/)[1]);
                                if (fileName === path) break;
                                var name = System.IO.Path.GetFileNameWithoutExtension(fileName);
                                var ext = System.IO.Path.GetExtension(fileName);
                                for (var i = 1; System.IO.File.Exists(fileName); ++i) {
                                    fileName = System.IO.Path.Combine(path, name + ' (' + i + ')' + ext);
                                }
                                index = bytesMatch(body, boundaryBytes, headEnd);
                                var fileStream = new System.IO.FileStream(fileName, System.IO.FileMode.CreateNew);
                                fileStream.Write(body, headEnd, index - headEnd);
                                fileStream.Close();
                            }
                        }

                        oSession.utilCreateResponseAndBypassServer();
                        oSession.responseCode = 303;
                        oSession.oResponse['Location'] = '.';
                    } else {
                        var body = '<html><body><style>*{font-family:monospace;font-size:16px}a{display:inline-block}label{cursor:pointer;color:#fff;background:#48b;padding:0 10px;margin:0 30px}label:hover{background:#3bf}</style>';
                        var dirs = path.split('/');
                        path = '';
                        for (var i in dirs) {
                            var dir = dirs[i];
                            if (dir) {
                                path += dir + '/';
                                body += '<a href="/' + path + '">' + dir + '\\</a>';
                            }
                        }
                        body += '<form method="post" enctype="multipart/form-data" style="display:inline-block">'
                            + '<label for="files" class="btn">Upload Files ....</label>'
                            + '<input style="visibility:hidden;position:absolute;" id="files" type="file" name="_" multiple onchange="form.submit();"/>'
                            + '</form><table>';
                        dirs = System.IO.Directory.GetDirectories(path);
                        for (var i in dirs) {
                            var dir = dirs[i];
                            var lastPath = dir.split('/').pop();
                            body += '<tr><td>&#x1f4c2;</td><td><a href="' + lastPath + '/">' + lastPath + '</a></td></tr>';
                        }
                        var files = System.IO.Directory.GetFiles(path);
                        for (var i in files) {
                            var file = files[i];
                            var fileName = file.split('/').pop();
                            var ext = fileName.split('.').pop();
                            var icon = { 'jpg':'&#128247;', 'png':'&#128247;', 'gif':'&#128247;', 'mp4':'&#127909;', 'mkv':'&#127909;',
                                'mp3':'&#127925;', 'html':'&lt;&gt;', 'json':'js', 'txt':'tx', 'exe':'&#128190;' }[ext]
                                || (ext.length === 2 ? ext : '&#128441;');
                            var fileSize = new System.IO.FileInfo(file).Length;
                            fileSize = (fileSize + '').replace(/\B(?=(\d{3})+(?!\d))/g, ",");
                            body += '<tr><td>' + icon + '</td><td><a href="' + fileName + '">' + fileName + '</a></td><td align="right">' + fileSize + '</td></tr>';
                        }
                    
                        body += '</table></body></html>';
    
                        oSession.utilCreateResponseAndBypassServer();
                        oSession.utilSetResponseBody(body);
                        oSession.oResponse['Content-Type'] = 'text/html; charset=utf-8';
                        oSession.utilGZIPResponse();
                        oSession.oResponse['Content-Encoding'] = 'gzip';
                    }
                } else {
                    found = false;
                }
                
                found && (oSession['ui-backcolor'] = 'tan');
            } catch (e) {
                oSession.oRequest['x-error'] = e;
                oSession['ui-backcolor'] = 'gold';
                
                oSession.utilCreateResponseAndBypassServer();
                oSession.utilSetResponseBody(e);
            }
        }
