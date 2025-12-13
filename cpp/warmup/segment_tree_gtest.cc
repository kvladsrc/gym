#include <gtest/gtest.h>

#include <algorithm>
#include <climits>
#include <vector>

#include "cpp/warmup/segment_tree.hpp"

TEST(SegmentTreeTest, OneElement) {
  std::vector<int> a = {1};
  warmup::segment_tree solver(a);
  auto result = solver.query(0, 0);
  EXPECT_EQ(result.maximal, 1);
  EXPECT_EQ(result.minimal, 1);
}

TEST(SegmentTreeTest, Simple) {
  std::vector<int> a = {7, 6, 45, 32, 6, 89, 67, 4, 6, 8};
  warmup::segment_tree solver(a);

  auto result = solver.query(0, 0);
  EXPECT_EQ(result.maximal, 7);
  EXPECT_EQ(result.minimal, 7);

  result = solver.query(0, 1);
  EXPECT_EQ(result.maximal, 7);
  EXPECT_EQ(result.minimal, 6);

  result = solver.query(2, 2);
  EXPECT_EQ(result.maximal, 45);
  EXPECT_EQ(result.minimal, 45);

  result = solver.query(0, 3);
  EXPECT_EQ(result.maximal, 45);
  EXPECT_EQ(result.minimal, 6);

  result = solver.query(5, 5);
  EXPECT_EQ(result.maximal, 89);
  EXPECT_EQ(result.minimal, 89);
}

TEST(SegmentTreeTest, AllEqualElements) {
  std::vector<int> a = {2, 2, 2, 2, 2};
  warmup::segment_tree solver(a);

  auto result = solver.query(0, 0);
  EXPECT_EQ(result.maximal, 2);
  EXPECT_EQ(result.minimal, 2);

  result = solver.query(1, 3);
  EXPECT_EQ(result.maximal, 2);
  EXPECT_EQ(result.minimal, 2);

  result = solver.query(0, 4);
  EXPECT_EQ(result.maximal, 2);
  EXPECT_EQ(result.minimal, 2);
}

TEST(SegmentTreeTest, FullRange) {
  std::vector<int> a = {5, 3, 8, 1, 9};
  warmup::segment_tree solver(a);

  auto result = solver.query(0, a.size() - 1);
  EXPECT_EQ(result.maximal, 9);
  EXPECT_EQ(result.minimal, 1);
}
