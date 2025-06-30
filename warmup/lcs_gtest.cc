#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/lcs.hpp"

TEST(LCSTest, EmptySequences) {
  std::vector<int> a = {};
  std::vector<int> b = {};
  EXPECT_EQ(warmup::lcs_rec(a, b), 0);
  EXPECT_EQ(warmup::lcs_dp(a, b), 0);
}

TEST(LCSTest, FirstNonEmptySecondEmpty) {
  std::vector<int> a = {1, 2, 3};
  std::vector<int> b = {};
  EXPECT_EQ(warmup::lcs_rec(a, b), 0);
  EXPECT_EQ(warmup::lcs_dp(a, b), 0);
}

TEST(LCSTest, NoCommonElements) {
  std::vector<int> a = {1, 2, 3};
  std::vector<int> b = {4, 5, 6};
  EXPECT_EQ(warmup::lcs_rec(a, b), 0);
  EXPECT_EQ(warmup::lcs_dp(a, b), 0);
}

TEST(LCSTest, CompleteMatch) {
  std::vector<int> a = {1, 2, 3, 4};
  std::vector<int> b = {1, 2, 3, 4};
  EXPECT_EQ(warmup::lcs_rec(a, b), 4);
  EXPECT_EQ(warmup::lcs_dp(a, b), 4);
}

TEST(LCSTest, PartialMatch) {
  std::vector<int> a = {1, 2, 3};
  std::vector<int> b = {2, 1, 3};
  EXPECT_EQ(warmup::lcs_rec(a, b), 2);
  EXPECT_EQ(warmup::lcs_dp(a, b), 2);
}

TEST(LCSTest, RepeatedElements) {
  std::vector<int> a = {1, 2, 3, 2, 1};
  std::vector<int> b = {1, 2, 1, 3, 1, 2, 1};
  EXPECT_EQ(warmup::lcs_rec(a, b), 5);
  EXPECT_EQ(warmup::lcs_dp(a, b), 5);
}
