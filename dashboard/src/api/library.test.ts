import {describe,it,expect} from 'vitest';
import {defaultGeneration,engineGeneration,generationRequest} from './library';
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
