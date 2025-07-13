#include "cpp/warmup/trie.hpp"

#include "gtest/gtest.h"

TEST(TrieTest, ExactMatch) {
  warmup::Trie trie;
  trie.insert("abc");
  EXPECT_TRUE(trie.contains("abc"));
}

TEST(TrieTest, DifferentWord) {
  warmup::Trie trie;
  trie.insert("abc");
  EXPECT_FALSE(trie.contains("abd"));
}

TEST(TrieTest, PrefixOnly) {
  warmup::Trie trie;
  trie.insert("abcd");
  EXPECT_TRUE(trie.contains("abc"));
}

TEST(TrieTest, LongerWord) {
  warmup::Trie trie;
  trie.insert("abc");
  EXPECT_FALSE(trie.contains("abcd"));
}

TEST(TrieTest, SingleLetter) {
  warmup::Trie trie;
  trie.insert("a");
  EXPECT_TRUE(trie.contains("a"));
}

TEST(TrieTest, CaseSensitive) {
  warmup::Trie trie;
  trie.insert("Hello");
  EXPECT_FALSE(trie.contains("hello"));
}

TEST(TrieTest, EmptyPattern) {
  warmup::Trie trie;
  trie.insert("abc");
  EXPECT_TRUE(trie.contains(""));
}

TEST(TrieTest, UnicodeSymbols) {
  warmup::Trie trie;
  trie.insert("привет");
  EXPECT_TRUE(trie.contains("привет"));
}

TEST(TrieTest, NonLatinMismatch) {
  warmup::Trie trie;
  trie.insert("привет");
  EXPECT_FALSE(trie.contains("пока"));
}
