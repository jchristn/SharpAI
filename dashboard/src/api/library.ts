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
export const generationFields=[
 ['Temperature','temperature',0,2,0.1],['MaxTokens','num_predict',1,32768,1],['TopP','top_p',0,1,0.05],['TopK','top_k',0,100,1],['MinP','min_p',0,1,0.05],
 ['RepeatPenalty','repeat_penalty',0,2,0.05],['RepeatLastN','repeat_last_n',0,32768,1],['PresencePenalty','presence_penalty',-2,2,0.1],['FrequencyPenalty','frequency_penalty',-2,2,0.1],['Seed','seed',0,2147483647,1],
] as const;
export function defaultGeneration():GenerationConfiguration {return {Temperature:0.6,MaxTokens:128,Stop:[],StreamResponse:true,JsonOutput:false,DisplayThinking:false};}
export function engineGeneration():GenerationConfiguration {return {Stop:[],StreamResponse:true,JsonOutput:false,DisplayThinking:false};}
export function generationRequest(configuration:GenerationConfiguration):{options:Record<string,unknown>;format?:string;stream:boolean} {
 const options:Record<string,unknown>={display_thinking:configuration.DisplayThinking};
 for(const [field,key] of generationFields){const value=configuration[field];if(value!=null)options[key]=value;}
 if(configuration.Stop.length)options.stop=configuration.Stop;
 return {options,...(configuration.JsonOutput?{format:'json'}:{}),stream:configuration.StreamResponse};
}
