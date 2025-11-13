#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/knapsack_with_duplicates.hpp"

TEST(KnapsackWithDuplicatesTest, EmptyArray) {
  std::vector<int> weights = {};
  std::vector<int> costs = {};
  EXPECT_EQ(warmup::knapsack_duplicates(weights, costs, 10), 0);
}

TEST(KnapsackWithDuplicatesTest, OneItemFitsRepeatedly) {
  std::vector<int> weights = {2};
  std::vector<int> costs = {3};
  EXPECT_EQ(warmup::knapsack_duplicates(weights, costs, 10), 15);
}

TEST(KnapsackWithDuplicatesTest, OneItemDoesNotFit) {
  std::vector<int> weights = {5};
  std::vector<int> costs = {1};
  EXPECT_EQ(warmup::knapsack_duplicates(weights, costs, 1), 0);
}

TEST(KnapsackWithDuplicatesTest, MultipleItemsWithDuplicates) {
  std::vector<int> weights = {3, 4};
  std::vector<int> costs = {4, 5};
  EXPECT_EQ(warmup::knapsack_duplicates(weights, costs, 10), 13);
}

TEST(KnapsackWithDuplicatesTest, OptimalCombinationWithRepeats) {
  std::vector<int> weights = {3, 5};
  std::vector<int> costs = {4, 7};
  EXPECT_EQ(warmup::knapsack_duplicates(weights, costs, 11), 15);
}

TEST(KnapsackWithDuplicatesTest, AllItemsTooHeavy) {
  std::vector<int> weights = {6, 7};
  std::vector<int> costs = {10, 13};
  EXPECT_EQ(warmup::knapsack_duplicates(weights, costs, 5), 0);
}

TEST(KnapsackWithDuplicatesTest, MultipleItems) {
  std::vector<int> weights = {2, 4, 1};
  std::vector<int> costs = {3, 1, 2};
  EXPECT_EQ(warmup::knapsack_duplicates(weights, costs, 9), 18);
}
