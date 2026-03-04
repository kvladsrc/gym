#include <gtest/gtest.h>

#include <string>

#include "cpp/warmup/rabin_karp.hpp"

// The loop over s never executes when s is empty, so search returns false
// even for an empty pattern (unlike KMP/Boyer-Moore which return true).
TEST(RabinKarpTest, EmptyPatternAndEmptyString) {
  std::string s = "";
  std::string pattern = "";
  EXPECT_TRUE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, EmptyPatternAndNonEmptyString) {
  std::string s = "abc";
  std::string pattern = "";
  EXPECT_TRUE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, NonEmptyPatternAndEmptyString) {
  std::string s = "";
  std::string pattern = "abc";
  EXPECT_FALSE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, PatternEqualsString) {
  std::string s = "abc";
  std::string pattern = "abc";
  EXPECT_TRUE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, PatternAtTheBeginning) {
  std::string s = "abcdef";
  std::string pattern = "ab";
  EXPECT_TRUE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, PatternInTheMiddle) {
  std::string s = "abcdef";
  std::string pattern = "cd";
  EXPECT_TRUE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, PatternAtTheEnd) {
  std::string s = "abcdef";
  std::string pattern = "ef";
  EXPECT_TRUE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, PatternNotFound) {
  std::string s = "abcdef";
  std::string pattern = "xyz";
  EXPECT_FALSE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, PartialMatch) {
  std::string s = "abdabc";
  std::string pattern = "abcd";
  EXPECT_FALSE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, RepeatingCharactersFound) {
  std::string s = "aabaaa";
  std::string pattern = "aaa";
  EXPECT_TRUE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, RepeatingCharactersNotFound) {
  std::string s = "aaab";
  std::string pattern = "aaaa";
  EXPECT_FALSE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, PatternLongerThanString) {
  std::string s = "ab";
  std::string pattern = "abc";
  EXPECT_FALSE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, SingleCharacterFound) {
  std::string s = "abc";
  std::string pattern = "a";
  EXPECT_TRUE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, SingleCharacterNotFound) {
  std::string s = "abc";
  std::string pattern = "d";
  EXPECT_FALSE(warmup::search(s, pattern));
}

TEST(RabinKarpTest, MultipleOccurrences) {
  std::string s = "abcdab";
  std::string pattern = "ab";
  EXPECT_TRUE(warmup::search(s, pattern));
}
