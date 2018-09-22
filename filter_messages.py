"""
Receive data from Pupil using ZMQ.
"""
import sys
import zmq
import time
import random
from msgpack import loads
sys.path.append(r'C:\Users\MS SocialNUI\Desktop\Downloads\IronPython.2.7.8\libs')
context = zmq.Context()
# open a req port to talk to pupil
addr = '127.0.0.1'  # remote ip or localhost
req_port = '50020'  # same as in the pupil remote gui
req = context.socket(zmq.REQ)
req.connect('tcp://{}:{}'.format(addr, req_port))
# ask for the sub port
req.send_string('SUB_PORT')
sub_port = req.recv_string()

# open a sub port to listen to pupil
sub = context.socket(zmq.SUB)
sub.connect('tcp://{}:{}'.format(addr, sub_port))

# set subscriptions to topics
# recv just pupil/gaze/notifications
sub.setsockopt(zmq.SUBSCRIBE, b'pupil.')
# sub.setsockopt_string(zmq.SUBSCRIBE, 'gaze')
# sub.setsockopt_string(zmq.SUBSCRIBE, 'notify.')
# sub.setsockopt_string(zmq.SUBSCRIBE, 'logging.')
# or everything:
# sub.setsockopt_string(zmq.SUBSCRIBE, '')
#print("\n{}: {}")
socket = context.socket(zmq.PUB)
socket.bind("tcp://*:12345")




while True:
    try:
        topic = sub.recv_string()
        msg = sub.recv()
        msg = loads(msg, encoding='utf-8')
        #socket.send_string('\n{}: {}'.format(topic, msg))
        print '\n{}: {}'.format(topic, msg)
        diameter = msg['diameter']
        timestamp = msg['timestamp']
        socket.send_string('{},{},{}'.format(topic, diameter, timestamp))


        time.sleep(0.5)
    except KeyboardInterrupt:
        break
