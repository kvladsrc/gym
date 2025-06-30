#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/knapsack.hpp"

TEST(KnapsackTest, EmptyArray) {
  std::vector<int> weights = {};
  std::vector<int> costs = {};
  EXPECT_EQ(warmup::knapsack(weights, costs, 10), 0);
}

TEST(KnapsackTest, OneItemFits) {
  std::vector<int> weights = {1};
  std::vector<int> costs = {1};
  EXPECT_EQ(warmup::knapsack(weights, costs, 1), 1);
}

TEST(KnapsackTest, OneItemDoesNotFit) {
  std::vector<int> weights = {10};
  std::vector<int> costs = {1};
  EXPECT_EQ(warmup::knapsack(weights, costs, 1), 0);
}

TEST(KnapsackTest, TwoItemsChooseBest) {
  std::vector<int> weights = {5, 7};
  std::vector<int> costs = {10, 13};
  EXPECT_EQ(warmup::knapsack(weights, costs, 10), 13);
}

TEST(KnapsackTest, Combination) {
  std::vector<int> weights = {5, 4, 6};
  std::vector<int> costs = {10, 40, 30};
  EXPECT_EQ(warmup::knapsack(weights, costs, 10), 70);
}

TEST(KnapsackTest, CapacityZero) {
  std::vector<int> weights = {2, 3, 4};
  std::vector<int> costs = {5, 6, 7};
  EXPECT_EQ(warmup::knapsack(weights, costs, 0), 0);
}

TEST(KnapsackTest, AllItemsTooHeavy) {
  std::vector<int> weights = {6, 7, 8};
  std::vector<int> costs = {10, 20, 30};
  EXPECT_EQ(warmup::knapsack(weights, costs, 5), 0);
}

TEST(KnapsackTest, DuplicateItems) {
  std::vector<int> weights = {2, 2, 2, 2};
  std::vector<int> costs = {1, 2, 3, 4};
  EXPECT_EQ(warmup::knapsack(weights, costs, 5), 7);
}

TEST(KnapsackTest, ExactFit) {
  std::vector<int> weights = {5, 5, 6};
  std::vector<int> costs = {10, 10, 15};
  EXPECT_EQ(warmup::knapsack(weights, costs, 11), 25);
}
