import {describe,it,expect} from 'vitest';
import {chatCommands,parseChatCommand} from './chatCommands';

describe('composer commands',()=>{
 it('recognizes every shared command and alias without sending it as a prompt',()=>{
  for(const command of chatCommands)for(const name of [command.name,...command.aliases])expect(parseChatCommand(' '+name.toUpperCase()+' ')).toMatchObject({isCommand:true,id:command.id,arguments:''});
 });
 it('retains model arguments and supports unknown commands and literal slash escaping',()=>{
  expect(parseChatCommand('/model example/model')).toMatchObject({id:'model',arguments:'example/model'});
  expect(parseChatCommand('/missing')).toMatchObject({id:'unknown',isCommand:true});
  expect(parseChatCommand('//literal')).toMatchObject({isCommand:false,prompt:'/literal'});
  expect(parseChatCommand('  ordinary prompt\n')).toMatchObject({isCommand:false,prompt:'  ordinary prompt\n'});
 });
});
