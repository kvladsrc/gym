#include <gtest/gtest.h>

#include <algorithm>
#include <vector>

#include "cpp/warmup/subsets.hpp"

using warmup::generate_subsets;

TEST(SubsetsTest, EmptySet) {
  std::vector<int> input;
  auto result = generate_subsets(input);
  ASSERT_EQ(result.size(), 1);
  EXPECT_TRUE(result[0].empty());
}

TEST(SubsetsTest, OneElement) {
  std::vector<int> input = {42};
  auto result = generate_subsets(input);
  ASSERT_EQ(result.size(), 2);
  // Order depends on implementation (bitmask 0 then 1)
  // 0: empty
  // 1: {42}
  EXPECT_TRUE(result[0].empty());
  ASSERT_EQ(result[1].size(), 1);
  EXPECT_EQ(result[1][0], 42);
}

TEST(SubsetsTest, TwoElements) {
  std::vector<int> input = {10, 20};
  auto result = generate_subsets(input);
  ASSERT_EQ(result.size(), 4);

  // Expected: [], [10], [20], [10, 20]
  // Based on bitmask loop:
  // 0 (00): []
  // 1 (01): [10]
  // 2 (10): [20]
  // 3 (11): [10, 20]

  EXPECT_TRUE(result[0].empty());

  ASSERT_EQ(result[1].size(), 1);
  EXPECT_EQ(result[1][0], 10);

  ASSERT_EQ(result[2].size(), 1);
  EXPECT_EQ(result[2][0], 20);

  ASSERT_EQ(result[3].size(), 2);
  EXPECT_EQ(result[3][0], 10);
  EXPECT_EQ(result[3][1], 20);
}

TEST(SubsetsTest, ThreeElements) {
  std::vector<int> input = {1, 2, 3};
  auto result = generate_subsets(input);
  ASSERT_EQ(result.size(), 8);

  // 000: []
  // 001: [1]
  // 010: [2]
  // 011: [1, 2]
  // 100: [3]
  // 101: [1, 3]
  // 110: [2, 3]
  // 111: [1, 2, 3]

  EXPECT_TRUE(result[0].empty());
  EXPECT_EQ(result[7].size(), 3);
}

TEST(SubsetsTest, TooLargeInput) {
  std::vector<int> input(64, 0);
  auto result = generate_subsets(input);
  EXPECT_TRUE(result.empty());
}
