#include "cpp/warmup/min_stack.hpp"

#include <climits>
#include <vector>

#include "gtest/gtest.h"

// Helper function to run the test logic from bats
std::vector<int> get_min_sequence(const std::vector<int>& inputs) {
  warmup::MinStack s;
  for (int el : inputs) {
    s.push(el);
  }

  std::vector<int> result;
  while (!s.empty()) {
    result.push_back(s.get_min());
    s.pop();
  }
  return result;
}

TEST(MinStackTest, EmptyStack) {
  std::vector<int> data = {};
  std::vector<int> expected = {};
  EXPECT_EQ(get_min_sequence(data), expected);
}

TEST(MinStackTest, OneElement) {
  std::vector<int> data = {5};
  std::vector<int> expected = {5};
  EXPECT_EQ(get_min_sequence(data), expected);
}

TEST(MinStackTest, TwoElementsDescending) {
  std::vector<int> data = {2, 1};
  std::vector<int> expected = {1, 2};
  EXPECT_EQ(get_min_sequence(data), expected);
}

TEST(MinStackTest, TwoElementsAscending) {
  std::vector<int> data = {1, 2};
  std::vector<int> expected = {1, 1};
  EXPECT_EQ(get_min_sequence(data), expected);
}

TEST(MinStackTest, ThreeElementsMixed) {
  std::vector<int> data = {3, 1, 2};
  std::vector<int> expected = {1, 1, 3};
  EXPECT_EQ(get_min_sequence(data), expected);
}

TEST(MinStackTest, Duplicates) {
  std::vector<int> data = {3, 1, 2, 1, 3};
  std::vector<int> expected = {1, 1, 1, 1, 3};
  EXPECT_EQ(get_min_sequence(data), expected);
}

TEST(MinStackTest, AllEqualElements) {
  std::vector<int> data = {2, 2, 2, 2};
  std::vector<int> expected = {2, 2, 2, 2};
  EXPECT_EQ(get_min_sequence(data), expected);
}

TEST(MinStackTest, DescendingOrder) {
  std::vector<int> data = {5, 4, 3, 2, 1};
  std::vector<int> expected = {1, 2, 3, 4, 5};
  EXPECT_EQ(get_min_sequence(data), expected);
}

TEST(MinStackTest, AscendingOrder) {
  std::vector<int> data = {1, 2, 3, 4, 5};
  std::vector<int> expected = {1, 1, 1, 1, 1};
  EXPECT_EQ(get_min_sequence(data), expected);
}

TEST(MinStackTest, NegativeNumbers) {
  std::vector<int> data = {0, -2, 3, -1};
  std::vector<int> expected = {-2, -2, -2, 0};
  EXPECT_EQ(get_min_sequence(data), expected);
}
