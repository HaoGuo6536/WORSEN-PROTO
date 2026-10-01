# ============================================================================
# test_candidate_manifest.py
# PURPOSE:
#   Check path-only audio candidate inputs without loading MOSS or Unity.
#   Invalid manifests must not point preparation outside the licensed Assets tree.
# ARCHITECTURAL ROLE:
#   Offline audio tooling test suite; outside Unity assemblies.
# KEY RESPONSIBILITIES:
#   - Verify candidate keys, catalogue shape and project-relative WAV paths.
# DEPENDENCIES:
#   Python unittest and prepare_roster_candidates in the pinned audio environment.
# USAGE NOTES:
#   Run with the local MOSS interpreter and -B; no inference or files are created.
# ============================================================================
import unittest

from prepare_roster_candidates import validate_candidates


class CandidateManifestTests(unittest.TestCase):
    def test_retains_exact_path_and_key(self):
        source = {"human-01": "Assets/External/Pack/Sound 01.WAV"}
        self.assertIs(validate_candidates(source), source)

    def test_rejects_empty_or_nonmapping_catalogues(self):
        for value in ({}, [], None, "Assets/Audio.wav"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                validate_candidates(value)

    def test_rejects_unsafe_or_nonwav_paths(self):
        for path in ("../sound.wav", "Assets/../sound.wav", "Assets//sound.wav",
                     "Assets/./sound.wav", "C:/Assets/sound.wav", "Assets/sound.wav:stream",
                     "Assets\\sound.wav", "Assets/sound.mp3", 12, None):
            with self.subTest(path=path), self.assertRaises(ValueError):
                validate_candidates({"sound": path})

    def test_rejects_unusable_output_keys(self):
        for key in ("", "../sound", "a/b", "MixedCase", "two words", 1):
            with self.subTest(key=key), self.assertRaises(ValueError):
                validate_candidates({key: "Assets/External/sound.wav"})


if __name__ == "__main__":
    unittest.main()
