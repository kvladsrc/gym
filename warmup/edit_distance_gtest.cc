#include <gtest/gtest.h>

#include <string>

#include "cpp/warmup/edit_distance.hpp"

TEST(EditDistanceTest, EmptyStrings) {
  EXPECT_EQ(warmup::edit_distance("", ""), 0);
}

TEST(EditDistanceTest, IdenticalStrings) {
  EXPECT_EQ(warmup::edit_distance("test", "test"), 0);
}

TEST(EditDistanceTest, InsertOperationsOnly) {
  EXPECT_EQ(warmup::edit_distance("abc", "abcde"), 2);
}

TEST(EditDistanceTest, DeleteOperationsOnly) {
  EXPECT_EQ(warmup::edit_distance("abcdef", "abc"), 3);
}

TEST(EditDistanceTest, ReplaceOperationsOnly) {
  EXPECT_EQ(warmup::edit_distance("abc", "xyz"), 3);
}

TEST(EditDistanceTest, MixedOperations) {
  EXPECT_EQ(warmup::edit_distance("intention", "execution"), 5);
}

TEST(EditDistanceTest, SingleEmptyAndNonEmptyString) {
  EXPECT_EQ(warmup::edit_distance("", "hello"), 5);
}

TEST(EditDistanceTest, OneCharacterDifference) {
  EXPECT_EQ(warmup::edit_distance("cat", "cut"), 1);
}

TEST(EditDistanceTest, ReverseStrings) {
  EXPECT_EQ(warmup::edit_distance("abcd", "dcba"), 4);
}

TEST(EditDistanceTest, LongerStringsWithMultipleEdits) {
  EXPECT_EQ(warmup::edit_distance("algorithm", "altruistic"), 6);
}
