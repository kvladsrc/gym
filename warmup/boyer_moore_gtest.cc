#include <gtest/gtest.h>

#include <map>
#include <string>

#include "cpp/warmup/boyer_moore.hpp"

TEST(BoyerMooreTest, EmptyPatternAndEmptyString) {
  std::string s = "";
  std::string pattern = "";
  EXPECT_TRUE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, EmptyPatternAndNonEmptyString) {
  std::string s = "abc";
  std::string pattern = "";
  EXPECT_TRUE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, NonEmptyPatternAndEmptyString) {
  std::string s = "";
  std::string pattern = "abc";
  EXPECT_FALSE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, PatternEqualsString) {
  std::string s = "abc";
  std::string pattern = "abc";
  EXPECT_TRUE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, PatternAtTheBeginning) {
  std::string s = "abcdef";
  std::string pattern = "ab";
  EXPECT_TRUE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, PatternInTheMiddle) {
  std::string s = "abcdef";
  std::string pattern = "cd";
  EXPECT_TRUE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, PatternAtTheEnd) {
  std::string s = "abcdef";
  std::string pattern = "ef";
  EXPECT_TRUE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, PatternNotFound) {
  std::string s = "abcdef";
  std::string pattern = "xyz";
  EXPECT_FALSE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, PartialMatch) {
  std::string s = "abdabc";
  std::string pattern = "abcd";
  EXPECT_FALSE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, RepeatingCharactersFound) {
  std::string s = "aabaaa";
  std::string pattern = "aaa";
  EXPECT_TRUE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, RepeatingCharactersNotFound) {
  std::string s = "aaab";
  std::string pattern = "aaaa";
  EXPECT_FALSE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, PatternLongerThanString) {
  std::string s = "ab";
  std::string pattern = "abc";
  EXPECT_FALSE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, SingleCharacterFound) {
  std::string s = "abc";
  std::string pattern = "a";
  EXPECT_TRUE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, SingleCharacterNotFound) {
  std::string s = "abc";
  std::string pattern = "d";
  EXPECT_FALSE(warmup::boyer_moore(s, pattern));
}

TEST(BoyerMooreTest, MultipleOccurrences) {
  std::string s = "abcdab";
  std::string pattern = "ab";
  EXPECT_TRUE(warmup::boyer_moore(s, pattern));
}
