"""仅用于本机 Edge CDP 验证的标准库 WebSocket 客户端。产品不依赖它。"""
import socket,struct,os,base64,hashlib,urllib.parse
class Connection:
    def __init__(self,url,timeout):
        u=urllib.parse.urlsplit(url)
        if u.scheme!='ws' or u.hostname not in ['127.0.0.1','localhost']:raise ValueError('只允许本机 CDP')
        self.s=socket.create_connection((u.hostname,u.port),timeout);self.pending=b''
        key=base64.b64encode(os.urandom(16)).decode()
        self.s.sendall(f'GET {u.path} HTTP/1.1\r\nHost: {u.hostname}:{u.port}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: {key}\r\n\r\n'.encode())
        response=b''
        while b'\r\n\r\n' not in response:response+=self.s.recv(4096)
        header,self.pending=response.split(b'\r\n\r\n',1)
        expected=base64.b64encode(hashlib.sha1((key+'258EAFA5-E914-47DA-95CA-C5AB0DC85B11').encode()).digest())
        if not header.startswith(b'HTTP/1.1 101') or expected not in header:self.s.close();raise OSError('CDP握手失败')
    def read(self,n):
        while len(self.pending)<n:
            data=self.s.recv(max(4096,n-len(self.pending)))
            if not data:raise EOFError('CDP连接已结束')
            self.pending+=data
        value,self.pending=self.pending[:n],self.pending[n:];return value
    def frame(self,opcode,payload):
        mask=os.urandom(4);n=len(payload)
        size=bytes([0x80|n]) if n<126 else bytes([0x80|126])+struct.pack('!H',n) if n<65536 else bytes([0x80|127])+struct.pack('!Q',n)
        self.s.sendall(bytes([0x80|opcode])+size+mask+bytes(b^mask[i%4] for i,b in enumerate(payload)))
    def send(self,text):self.frame(1,text.encode('utf-8'))
    def recv(self):
        parts=[]
        while True:
            first,second=self.read(2);n=second&127
            if n==126:n=struct.unpack('!H',self.read(2))[0]
            if n==127:n=struct.unpack('!Q',self.read(8))[0]
            if n>16_000_000:raise OSError('CDP消息过大')
            mask=self.read(4) if second&128 else None;body=self.read(n)
            if mask:body=bytes(b^mask[i%4] for i,b in enumerate(body))
            opcode=first&15
            if opcode==8:raise EOFError('CDP连接已关闭')
            if opcode==9:self.frame(10,body);continue
            if opcode==10:continue
            if opcode not in [0,1]:raise OSError('不支持的CDP帧')
            parts.append(body)
            if first&128:return b''.join(parts).decode('utf-8')
    def close(self):
        try:self.frame(8,b'')
        finally:self.s.close()
def create_connection(url,timeout=30,**kwargs):return Connection(url,timeout)
