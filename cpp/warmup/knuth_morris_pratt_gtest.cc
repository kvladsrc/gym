#include <gtest/gtest.h>

#include <string>

#include "cpp/warmup/knuth_morris_pratt.hpp"

TEST(KnuthMorrisPrattTest, EmptyPatternAndEmptyString) {
  std::string s = "";
  std::string pattern = "";
  EXPECT_TRUE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, EmptyPatternAndNonEmptyString) {
  std::string s = "abc";
  std::string pattern = "";
  EXPECT_TRUE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, NonEmptyPatternAndEmptyString) {
  std::string s = "";
  std::string pattern = "abc";
  EXPECT_FALSE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, PatternEqualsString) {
  std::string s = "abc";
  std::string pattern = "abc";
  EXPECT_TRUE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, PatternAtTheBeginning) {
  std::string s = "abcdef";
  std::string pattern = "ab";
  EXPECT_TRUE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, PatternInTheMiddle) {
  std::string s = "abcdef";
  std::string pattern = "cd";
  EXPECT_TRUE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, PatternAtTheEnd) {
  std::string s = "abcdef";
  std::string pattern = "ef";
  EXPECT_TRUE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, PatternNotFound) {
  std::string s = "abcdef";
  std::string pattern = "xyz";
  EXPECT_FALSE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, PartialMatch) {
  std::string s = "abdabc";
  std::string pattern = "abcd";
  EXPECT_FALSE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, RepeatingCharactersFound) {
  std::string s = "aabaaa";
  std::string pattern = "aaa";
  EXPECT_TRUE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, RepeatingCharactersNotFound) {
  std::string s = "aaab";
  std::string pattern = "aaaa";
  EXPECT_FALSE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, PatternLongerThanString) {
  std::string s = "ab";
  std::string pattern = "abc";
  EXPECT_FALSE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, SingleCharacterFound) {
  std::string s = "abc";
  std::string pattern = "a";
  EXPECT_TRUE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, SingleCharacterNotFound) {
  std::string s = "abc";
  std::string pattern = "d";
  EXPECT_FALSE(warmup::kmp(s, pattern));
}

TEST(KnuthMorrisPrattTest, MultipleOccurrences) {
  std::string s = "abcdab";
  std::string pattern = "ab";
  EXPECT_TRUE(warmup::kmp(s, pattern));
}
