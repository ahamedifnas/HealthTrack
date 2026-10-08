// Local test-only SMTP sink. Messages stay in memory and are never logged or sent externally.
const net = require('net');
const http = require('http');
const messages = [];
let fail = false;
net.createServer(socket => {
  socket.write('220 localhost HealthTrack test SMTP\r\n');
  let buffer = '', data = false, body = '';
  socket.on('data', chunk => {
    buffer += chunk.toString();
    let end;
    while ((end = buffer.indexOf('\r\n')) >= 0) {
      const line = buffer.slice(0, end); buffer = buffer.slice(end + 2);
      if (data) {
        if (line === '.') { messages.push(body); body = ''; data = false; socket.write('250 accepted\r\n'); }
        else body += line + '\r\n';
      } else if (/^EHLO|^HELO/.test(line)) socket.write('250 localhost\r\n');
      else if (/^DATA/.test(line)) {
        if (fail) socket.write('451 temporary test failure\r\n');
        else { data = true; socket.write('354 send message\r\n'); }
      } else if (/^QUIT/.test(line)) socket.end('221 bye\r\n');
      else socket.write('250 ok\r\n');
    }
  });
  socket.on('error', () => {});
}).listen(2526, '127.0.0.1');
http.createServer((req, res) => {
  if (req.url === '/fail') fail = true;
  if (req.url === '/resume') fail = false;
  res.setHeader('Content-Type', 'application/json');
  res.end(JSON.stringify(messages));
}).listen(2527, '127.0.0.1');
console.log('Local SMTP test fixture ready.');
