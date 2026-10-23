import catalog from './chatCommands.json';

export const chatCommands=catalog.commands;
export const compactInstruction=catalog.compactInstruction;
export const summaryPrefix=catalog.summaryPrefix;
export const compactMaxTokens=catalog.compactMaxTokens;
export interface ChatCommand {isCommand:boolean;id:string;arguments:string;prompt:string;}
/** Commands are composer actions. Double slash escapes a literal leading slash. */
export function parseChatCommand(input:string):ChatCommand {
 const trimmed=input.trim();
 if(trimmed.startsWith('//'))return {isCommand:false,id:'',arguments:'',prompt:trimmed.slice(1)};
 if(!trimmed.startsWith('/'))return {isCommand:false,id:'',arguments:'',prompt:input};
 const separator=trimmed.search(/\s/);
 const name=(separator<0?trimmed:trimmed.slice(0,separator)).toLowerCase();
 const args=separator<0?'':trimmed.slice(separator).trim();
 const command=chatCommands.find(item=>item.name===name || item.aliases.includes(name));
 return {isCommand:true,id:command?.id ?? 'unknown',arguments:args,prompt:trimmed};
}
