#include <gtest/gtest.h>

#include <algorithm>
#include <random>
#include <vector>

#include "cpp/warmup/sparse_table.hpp"

namespace warmup {

TEST(SparseTableTest, SmallRange) {
  std::vector<int> data = {1, 5, 2, 4, 3};
  sparse_table st(data);

  EXPECT_EQ(st.query(0, 0), 1);
  EXPECT_EQ(st.query(0, 1), 1);
  EXPECT_EQ(st.query(0, 2), 1);
  EXPECT_EQ(st.query(1, 2), 2);
  EXPECT_EQ(st.query(1, 3), 2);
  EXPECT_EQ(st.query(2, 4), 2);
  EXPECT_EQ(st.query(0, 4), 1);
}

TEST(SparseTableTest, RandomData) {
  std::mt19937 rng(42);
  std::vector<int> data(100);
  std::generate(data.begin(), data.end(), [&]() { return rng() % 1000; });

  sparse_table st(data);

  for (int i = 0; i < 100; ++i) {
    for (int j = i; j < 100; ++j) {
      int expected = data[i];
      for (int k = i + 1; k <= j; ++k) {
        expected = std::min(expected, data[k]);
      }
      EXPECT_EQ(st.query(i, j), expected) << "Range [" << i << ", " << j << "]";
    }
  }
}

}  // namespace warmup
