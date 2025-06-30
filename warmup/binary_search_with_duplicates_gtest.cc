#include <gtest/gtest.h>

#include <optional>
#include <vector>

#include "cpp/warmup/binary_search_with_duplicates.hpp"

TEST(BinarySearchWithDuplicatesTest, EmptyArray) {
  std::vector<int> data = {};
  EXPECT_EQ(warmup::lower_bound(data, 3), std::nullopt);
}

TEST(BinarySearchWithDuplicatesTest, CaseFound) {
  std::vector<int> data = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
  EXPECT_EQ(warmup::lower_bound(data, 6), 5);
}

TEST(BinarySearchWithDuplicatesTest, CaseAllBigger) {
  std::vector<int> data = {3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17};
  EXPECT_EQ(warmup::lower_bound(data, 2), 0);
}

TEST(BinarySearchWithDuplicatesTest, CaseAllSmaller) {
  std::vector<int> data = {3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17};
  EXPECT_EQ(warmup::lower_bound(data, 20), std::nullopt);
}

TEST(BinarySearchWithDuplicatesTest, CaseDuplicates) {
  std::vector<int> data = {1, 2, 3, 4, 5, 5, 5, 5, 5, 6, 7, 7, 8, 8, 9};
  EXPECT_EQ(warmup::lower_bound(data, 5), 4);
}

TEST(BinarySearchWithDuplicatesTest, CaseBound) {
  std::vector<int> data = {1, 2, 3, 4, 5, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8};
  EXPECT_EQ(warmup::lower_bound(data, 7), 5);
}

TEST(BinarySearchWithDuplicatesTest, TargetAtBeginning) {
  std::vector<int> data = {1, 2, 3, 4, 5};
  EXPECT_EQ(warmup::lower_bound(data, 1), 0);
}

TEST(BinarySearchWithDuplicatesTest, TargetAtEnd) {
  std::vector<int> data = {1, 2, 3, 4, 5};
  EXPECT_EQ(warmup::lower_bound(data, 5), 4);
}

TEST(BinarySearchWithDuplicatesTest, TargetInMiddleNotFound) {
  std::vector<int> data = {1, 3, 5, 7, 9, 11};
  EXPECT_EQ(warmup::lower_bound(data, 6), 3);
}

TEST(BinarySearchWithDuplicatesTest, AllEqualElements) {
  std::vector<int> data = {5, 5, 5, 5, 5};
  EXPECT_EQ(warmup::lower_bound(data, 5), 0);
}
