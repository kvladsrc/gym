#include <climits>
#include <vector>

#include "cpp/warmup/min_deque.hpp"
#include "gtest/gtest.h"

// Helper function to simulate sliding window minimum
std::vector<int> get_sliding_window_min(const std::vector<int>& inputs,
                                        size_t window_size) {
  warmup::MinDeque d(window_size);
  std::vector<int> result;
  for (int el : inputs) {
    d.push_back(el);
    result.push_back(d.get_min());
  }
  return result;
}

TEST(MinDequeTest, EmptyInput) {
  std::vector<int> data = {};
  std::vector<int> expected = {};
  EXPECT_EQ(get_sliding_window_min(data, 3), expected);
}

TEST(MinDequeTest, WindowSizeOne) {
  std::vector<int> data = {1, 3, 2, 5, 4};
  std::vector<int> expected = {1, 3, 2, 5, 4};
  EXPECT_EQ(get_sliding_window_min(data, 1), expected);
}

TEST(MinDequeTest, WindowSizeLargerThanInput) {
  std::vector<int> data = {3, 1, 2};
  // Window size 5.
  // push 3: min 3
  // push 1: min 1
  // push 2: min 1
  std::vector<int> expected = {3, 1, 1};
  EXPECT_EQ(get_sliding_window_min(data, 5), expected);
}

TEST(MinDequeTest, SlidingWindowBasic) {
  std::vector<int> data = {2, 1, 3, 4, 6, 3, 8, 9, 10, 12, 56};
  // Window size 4
  // 2: {2} -> min 2
  // 1: {2, 1} -> min 1
  // 3: {2, 1, 3} -> min 1
  // 4: {2, 1, 3, 4} -> min 1
  // 6: {1, 3, 4, 6} -> min 1 (2 expired)
  // 3: {3, 4, 6, 3} -> min 3 (1 expired)
  // 8: {4, 6, 3, 8} -> min 3
  // 9: {6, 3, 8, 9} -> min 3
  // 10: {3, 8, 9, 10} -> min 3
  // 12: {8, 9, 10, 12} -> min 8 (3 expired)
  // 56: {9, 10, 12, 56} -> min 9
  std::vector<int> expected = {2, 1, 1, 1, 1, 3, 3, 3, 3, 8, 9};
  EXPECT_EQ(get_sliding_window_min(data, 4), expected);
}

TEST(MinDequeTest, DescendingOrder) {
  std::vector<int> data = {5, 4, 3, 2, 1};
  // Window size 3
  // 5: {5} -> 5
  // 4: {5, 4} -> 4
  // 3: {5, 4, 3} -> 3
  // 2: {4, 3, 2} -> 2
  // 1: {3, 2, 1} -> 1
  std::vector<int> expected = {5, 4, 3, 2, 1};
  EXPECT_EQ(get_sliding_window_min(data, 3), expected);
}

TEST(MinDequeTest, AscendingOrder) {
  std::vector<int> data = {1, 2, 3, 4, 5};
  // Window size 3
  // 1: {1} -> 1
  // 2: {1, 2} -> 1
  // 3: {1, 2, 3} -> 1
  // 4: {2, 3, 4} -> 2
  // 5: {3, 4, 5} -> 3
  std::vector<int> expected = {1, 1, 1, 2, 3};
  EXPECT_EQ(get_sliding_window_min(data, 3), expected);
}

TEST(MinDequeTest, Duplicates) {
  std::vector<int> data = {2, 2, 2, 2};
  // Window size 2
  std::vector<int> expected = {2, 2, 2, 2};
  EXPECT_EQ(get_sliding_window_min(data, 2), expected);
}

TEST(MinDequeTest, InitialEmptyCheck) {
  warmup::MinDeque d(5);
  EXPECT_EQ(d.get_min(), INT_MAX);
}
