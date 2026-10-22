"""Consumer contract checks against the operations harness's real local server."""
import os
import pathlib
import sys
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / 'src'))
from sharpai import SharpAIClient, SharpAIError


@unittest.skipUnless(os.environ.get('SHARPAI_TEST_SERVER_URL'), 'Requires live operations server and ops-story model')
class LiveOperationsTests(unittest.TestCase):
    def setUp(self):
        self.client = SharpAIClient(os.environ['SHARPAI_TEST_SERVER_URL'], timeout=15)

    def test_ollama_counts_and_nanosecond_timings_survive_sdk(self):
        for chat in [False, True]:
            with self.subTest(chat=chat):
                response = (self.client.chat('ops-story', [{'role':'user', 'content':'Once upon a time'}], options={'num_predict':4, 'temperature':0})
                    if chat else self.client.generate('ops-story', 'Once upon a time', options={'num_predict':4, 'temperature':0}))
                self.assertTrue(response['done'])
                self.assertEqual(4, response['eval_count'])
                for key in ['prompt_eval_count', 'prompt_eval_duration', 'eval_duration', 'total_duration']:
                    self.assertGreater(response[key], 0)

    def test_openai_usage_survives_sdk(self):
        for chat in [False, True]:
            with self.subTest(chat=chat):
                response = (self.client.openai_chat('ops-story', [{'role':'user', 'content':'Once upon a time'}], max_tokens=4, temperature=0)
                    if chat else self.client.openai_completions('ops-story', 'Once upon a time', max_tokens=4, temperature=0))
                usage = response['usage']
                self.assertEqual(4, usage['completion_tokens'])
                self.assertGreater(usage['prompt_tokens'], 0)
                self.assertEqual(usage['prompt_tokens'] + 4, usage['total_tokens'])

    def test_context_overflow_is_a_typed_sdk_error_and_does_not_break_next_call(self):
        with self.assertRaises(SharpAIError) as raised:
            self.client.openai_completions('ops-story', 'Once upon a time', max_tokens=100000)
        self.assertEqual(400, raised.exception.status)
        self.assertTrue(raised.exception.body)
        result = self.client.openai_completions('ops-story', 'Once upon a time', max_tokens=2, temperature=0)
        self.assertEqual(2, result['usage']['completion_tokens'])
