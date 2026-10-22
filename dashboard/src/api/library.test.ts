import {describe,it,expect} from 'vitest';
import {clampGeneration,clampGenerationValue,defaultGeneration,engineGeneration,generationFields,generationRequest} from './library';
describe('saved generation configurations',()=>{
 it('maps every sampling field and preserves explicit zero and false values',()=>{
  const result=generationRequest({...defaultGeneration(),Temperature:0,MaxTokens:256,TopP:0,TopK:0,MinP:0,RepeatPenalty:0,RepeatLastN:0,PresencePenalty:-1,FrequencyPenalty:1,Seed:0,Stop:['END',' '],StreamResponse:false,JsonOutput:true,DisplayThinking:true});
  expect(result).toEqual({stream:false,format:'json',options:{temperature:0,num_predict:256,top_p:0,top_k:0,min_p:0,repeat_penalty:0,repeat_last_n:0,presence_penalty:-1,frequency_penalty:1,seed:0,stop:['END',' '],display_thinking:true}});
 });
 it('uses actual engine defaults when no configuration is selected',()=>{
  expect(generationRequest(engineGeneration())).toEqual({stream:true,options:{display_thinking:false}});
  expect(generationRequest({...defaultGeneration(),Temperature:null})).not.toHaveProperty('options.temperature');
 });
});

describe('numeric generation bounds',()=>{
 for(const field of generationFields){
  it(`clamps ${field.field} and keeps engine defaults available`,()=>{
   expect(clampGenerationValue(field,field.min-100)).toBe(field.min);
   expect(clampGenerationValue(field,field.max+100)).toBe(field.max);
   expect(clampGenerationValue(field,null)).toBeNull();
   expect(clampGenerationValue(field,Number.NaN)).toBeNull();
   expect(clampGenerationValue(field,Number.POSITIVE_INFINITY)).toBeNull();
   if(field.step===1)expect(Number.isInteger(clampGenerationValue(field,field.min+1.5))).toBe(true);
  });
 }
 it('normalizes the complete saved configuration without changing its source',()=>{
  const input={...defaultGeneration(),Temperature:4,MaxTokens:-3,TopK:22.7,TopP:.333,Seed:2147483648};
  expect(clampGeneration(input)).toMatchObject({Temperature:2,MaxTokens:1,TopK:23,TopP:.333,Seed:2147483647});
  expect(input.Temperature).toBe(4);
 });
});
