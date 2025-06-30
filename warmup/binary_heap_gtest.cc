#include <gtest/gtest.h>

#include <algorithm>
#include <vector>

#include "cpp/warmup/binary_heap.hpp"

TEST(BinaryHeapTest, Empty) {
  warmup::BinaryHeap heap;
  EXPECT_TRUE(heap.empty());
  EXPECT_EQ(heap.top(), -1);
}

TEST(BinaryHeapTest, OneElement) {
  warmup::BinaryHeap heap;
  heap.push(1);
  EXPECT_FALSE(heap.empty());
  EXPECT_EQ(heap.top(), 1);
  heap.pop();
  EXPECT_TRUE(heap.empty());
}

TEST(BinaryHeapTest, AlreadySorted) {
  warmup::BinaryHeap heap;
  std::vector<int> input = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
  for (int val : input) {
    heap.push(val);
  }
  std::vector<int> result;
  while (!heap.empty()) {
    result.push_back(heap.top());
    heap.pop();
  }
  EXPECT_EQ(result, input);
}

TEST(BinaryHeapTest, AlreadySortedReversed) {
  warmup::BinaryHeap heap;
  std::vector<int> input = {10, 9, 8, 7, 6, 5, 4, 3, 2, 1};
  for (int val : input) {
    heap.push(val);
  }
  std::vector<int> expected = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
  std::vector<int> result;
  while (!heap.empty()) {
    result.push_back(heap.top());
    heap.pop();
  }
  EXPECT_EQ(result, expected);
}

TEST(BinaryHeapTest, Duplicates) {
  warmup::BinaryHeap heap;
  std::vector<int> input = {1, 3, 3, 3, 3, 3, 3, 3, 4, 2};
  for (int val : input) {
    heap.push(val);
  }
  std::vector<int> expected = {1, 2, 3, 3, 3, 3, 3, 3, 3, 4};
  std::vector<int> result;
  while (!heap.empty()) {
    result.push_back(heap.top());
    heap.pop();
  }
  EXPECT_EQ(result, expected);
}

TEST(BinaryHeapTest, AllEqualElements) {
  warmup::BinaryHeap heap;
  std::vector<int> input = {1, 1, 1, 1, 1};
  for (int val : input) {
    heap.push(val);
  }
  std::vector<int> expected = {1, 1, 1, 1, 1};
  std::vector<int> result;
  while (!heap.empty()) {
    result.push_back(heap.top());
    heap.pop();
  }
  EXPECT_EQ(result, expected);
}

TEST(BinaryHeapTest, RandomOrder) {
  warmup::BinaryHeap heap;
  std::vector<int> input = {4, 1, 3, 2, 6, 5};
  for (int val : input) {
    heap.push(val);
  }
  std::vector<int> expected = {1, 2, 3, 4, 5, 6};
  std::vector<int> result;
  while (!heap.empty()) {
    result.push_back(heap.top());
    heap.pop();
  }
  EXPECT_EQ(result, expected);
}

TEST(BinaryHeapTest, NegativeNumbers) {
  warmup::BinaryHeap heap;
  std::vector<int> input = {-3, -1, -2, -4, 0, 5};
  for (int val : input) {
    heap.push(val);
  }
  std::vector<int> expected = {-4, -3, -2, -1, 0, 5};
  std::vector<int> result;
  while (!heap.empty()) {
    result.push_back(heap.top());
    heap.pop();
  }
  EXPECT_EQ(result, expected);
}

TEST(BinaryHeapTest, OneElementNegative) {
  warmup::BinaryHeap heap;
  heap.push(-1);
  EXPECT_FALSE(heap.empty());
  EXPECT_EQ(heap.top(), -1);
  heap.pop();
  EXPECT_TRUE(heap.empty());
}
