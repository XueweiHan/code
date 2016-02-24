    static function OnBeforeRequest(oSession: Session) {

        // simple http file server
        if (oSession.RequestMethod == 'GET' &&
            (oSession.HostnameIs(System.Environment.MachineName) || oSession.HostnameIs('localhost') || oSession.HostnameIs('f.cn'))) {
            try {
                var path = decodeURIComponent(oSession.PathAndQuery).substring(1);//.Insert(1, ":");
                oSession.oRequest['x-filepath'] = path;
    
                var responseBody = null;
                var responseFile = null;
                var contentType = null;
                if (System.IO.File.Exists(path)) {
                    
                    responseFile = path;
                    
                    var fileExt = path.split('.').pop();
                    contentType = { 'cmd': 'text/plain' }[fileExt];
    
                } else if (System.IO.Directory.Exists(path)) {
                    var body = '<html><body><style>*{font-family:monospace;font-size:16px}p a{margin:0}</style><p>';
                    var pathEndWithSlash = path.EndsWith('/');
                    var dirs = path.split('/');
                    path = '';
                    for (var i in dirs) {
                        var dir = dirs[i];
                        if (dir) {
                            path += dir + '/';
                            body += '<a href="/' + path + '">' + dir + '\\</a>';
                        }
                    }
                    body += '</p><table>';
                    dirs = System.IO.Directory.GetDirectories(path);
                    for (var i in dirs) {
                        var dir = dirs[i];
                        var lastPath = dir.split('/').pop();
                        body += '<tr><td>&#x1f4c2;</td><td><a href="' + (pathEndWithSlash ? lastPath : '/' + dir) + '/">' + lastPath + '</a></td></tr>';
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
                        body += '<tr><td>' + icon + '</td><td><a href="' + (pathEndWithSlash ? fileName : '/' + file) + '">' + fileName + '</a></td><td align="right">' + fileSize + '</td></tr>';
                    }
                    
                    body += '</table></body></html>';
    
                    responseBody = body;
                    contentType = 'text/html; charset=utf-8';
                }
            
                if (responseBody != null || responseFile != null) {
                    oSession.utilCreateResponseAndBypassServer();
                    if (responseBody != null) {
                        oSession.utilSetResponseBody(responseBody);
                    } else {
                        oSession.LoadResponseFromFile(responseFile);
                    }
                    if (contentType) {
                        oSession.oResponse['Content-Type'] = contentType;
                    }
                    oSession.utilGZIPResponse();
                    oSession.oResponse['Content-Encoding'] = 'gzip';
        
                    oSession['ui-backcolor'] = 'tan';
                }
            } catch (e) {
                oSession.oRequest['x-error'] = e;
                oSession['ui-backcolor'] = 'gold';
            }
        }
