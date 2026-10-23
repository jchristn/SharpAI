export type LibraryKind='system-prompts'|'model-configurations';
export type LibraryScope='System'|'Tenant'|'User';
export interface GenerationConfiguration {
 Temperature?:number|null; MaxTokens?:number|null; TopP?:number|null; TopK?:number|null; MinP?:number|null;
 RepeatPenalty?:number|null; RepeatLastN?:number|null; PresencePenalty?:number|null; FrequencyPenalty?:number|null; Seed?:number|null;
 Stop:string[];StreamResponse:boolean;JsonOutput:boolean;DisplayThinking:boolean;
}
export interface LibraryItem {
 Id:string;Kind:string;Name:string;Description:string;Scope:LibraryScope;TenantId:string|null;UserId:string|null;
 Content:string|null;Configuration:GenerationConfiguration|null;CreatedUtc:string;UpdatedUtc:string;CanEdit:boolean;CanDelete:boolean;
}
export interface LibraryContext {IsAdmin:boolean;IsTenantAdmin:boolean;TenantId:string|null;UserId:string|null;UserTenantId:string|null;}
export interface GenerationField {field: keyof Pick<GenerationConfiguration, "Temperature"|"MaxTokens"|"TopP"|"TopK"|"MinP"|"RepeatPenalty"|"RepeatLastN"|"PresencePenalty"|"FrequencyPenalty"|"Seed">;key:string;min:number;max:number;step:number;}
export const generationFields:GenerationField[]=[
  {field:'Temperature',key:'temperature',min:0,max:2,step:0.1},
  {field:'MaxTokens',key:'num_predict',min:1,max:32768,step:1},
  {field:'TopP',key:'top_p',min:0,max:1,step:0.05},
  {field:'TopK',key:'top_k',min:0,max:100,step:1},
  {field:'MinP',key:'min_p',min:0,max:1,step:0.05},
  {field:'RepeatPenalty',key:'repeat_penalty',min:0,max:2,step:0.05},
  {field:'RepeatLastN',key:'repeat_last_n',min:0,max:32768,step:1},
  {field:'PresencePenalty',key:'presence_penalty',min:-2,max:2,step:0.1},
  {field:'FrequencyPenalty',key:'frequency_penalty',min:-2,max:2,step:0.1},
  {field:'Seed',key:'seed',min:0,max:2147483647,step:1},
];
export function defaultGeneration():GenerationConfiguration {return {Temperature:0.6,MaxTokens:2048,Stop:[],StreamResponse:true,JsonOutput:false,DisplayThinking:false};}
export function engineGeneration():GenerationConfiguration {return {Stop:[],StreamResponse:true,JsonOutput:false,DisplayThinking:false};}
export function generationRequest(configuration:GenerationConfiguration):{options:Record<string,unknown>;format?:string;stream:boolean} {
 const options:Record<string,unknown>={display_thinking:configuration.DisplayThinking};
 for(const {field,key} of generationFields){const value=configuration[field];if(value!=null)options[key]=value;}
 if(configuration.Stop.length)options.stop=configuration.Stop;
 return {options,...(configuration.JsonOutput?{format:'json'}:{}),stream:configuration.StreamResponse};
}

/** Keep editor values within the server contract; blank/non-finite values use engine defaults. */
export function clampGenerationValue(field:GenerationField,value:number|null|undefined):number|null {
 if(value==null||!Number.isFinite(value))return null;
 const bounded=Math.max(field.min,Math.min(field.max,value));
 return field.step===1?Math.round(bounded):bounded;
}
export function clampGeneration(configuration:GenerationConfiguration):GenerationConfiguration {
 const normalized={...configuration};
 for(const field of generationFields)normalized[field.field]=clampGenerationValue(field,configuration[field.field]);
 return normalized;
}
