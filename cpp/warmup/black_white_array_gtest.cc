#include <gtest/gtest.h>

#include "cpp/warmup/black_white_array.hpp"

static void insert_all(bwarray& arr, std::initializer_list<int> vals) {
  for (int v : vals) {
    arr.insert(v);
  }
}

TEST(BwArrayTest, SizeAfterInserts) {
  bwarray arr;
  EXPECT_EQ(arr.size(), 0u);
  arr.insert(1);
  EXPECT_EQ(arr.size(), 1u);
  arr.insert(2);
  EXPECT_EQ(arr.size(), 2u);
  arr.insert(3);
  EXPECT_EQ(arr.size(), 3u);
}

TEST(BwArrayTest, InsertManyElements) {
  bwarray arr;
  for (int i = 0; i < 16; ++i) {
    arr.insert(i);
  }
  EXPECT_EQ(arr.size(), 16u);
}

TEST(BwArrayTest, SearchNotFound) {
  bwarray arr;
  EXPECT_FALSE(arr.search(42));
}

TEST(BwArrayTest, SearchAfterInsert) {
  bwarray arr;
  arr.insert(10);
  EXPECT_TRUE(arr.search(10));
  EXPECT_FALSE(arr.search(11));
}

TEST(BwArrayTest, SearchMultipleElements) {
  bwarray arr;
  insert_all(arr, {5, 3, 9, 1});
  EXPECT_TRUE(arr.search(5));
  EXPECT_TRUE(arr.search(3));
  EXPECT_TRUE(arr.search(9));
  EXPECT_TRUE(arr.search(1));
  EXPECT_FALSE(arr.search(2));
}

TEST(BwArrayTest, SearchDuplicates) {
  bwarray arr;
  arr.insert(7);
  arr.insert(7);
  EXPECT_TRUE(arr.search(7));
}

TEST(BwArrayTest, RemoveNotPresent) {
  bwarray arr;
  arr.insert(1);
  arr.remove(42);
  EXPECT_EQ(arr.size(), 1u);
  EXPECT_TRUE(arr.search(1));
}

TEST(BwArrayTest, RemoveSingleElement) {
  bwarray arr;
  arr.insert(5);
  EXPECT_EQ(arr.size(), 1u);
  arr.remove(5);
  EXPECT_EQ(arr.size(), 0u);
  EXPECT_FALSE(arr.search(5));
}

TEST(BwArrayTest, RemoveDecreaseSize) {
  bwarray arr;
  insert_all(arr, {1, 2, 3, 4});
  EXPECT_EQ(arr.size(), 4u);
  arr.remove(2);
  EXPECT_EQ(arr.size(), 3u);
  EXPECT_FALSE(arr.search(2));
  EXPECT_TRUE(arr.search(1));
  EXPECT_TRUE(arr.search(3));
  EXPECT_TRUE(arr.search(4));
}

TEST(BwArrayTest, RemoveDuplicateOnlyOnce) {
  bwarray arr;
  arr.insert(7);
  arr.insert(7);
  EXPECT_EQ(arr.size(), 2u);
  arr.remove(7);
  EXPECT_EQ(arr.size(), 1u);
  EXPECT_TRUE(arr.search(7));
}

TEST(BwArrayTest, RemoveAllElements) {
  bwarray arr;
  insert_all(arr, {10, 20, 30});
  arr.remove(10);
  arr.remove(20);
  arr.remove(30);
  EXPECT_EQ(arr.size(), 0u);
  EXPECT_FALSE(arr.search(10));
  EXPECT_FALSE(arr.search(20));
  EXPECT_FALSE(arr.search(30));
}

TEST(BwArrayTest, InsertAfterRemove) {
  bwarray arr;
  arr.insert(1);
  arr.remove(1);
  arr.insert(1);
  EXPECT_EQ(arr.size(), 1u);
  EXPECT_TRUE(arr.search(1));
}

TEST(BwArrayTest, MixedOperations) {
  bwarray arr;
  for (int i = 0; i < 8; ++i) {
    arr.insert(i);
  }
  EXPECT_EQ(arr.size(), 8u);
  arr.remove(3);
  arr.remove(5);
  EXPECT_EQ(arr.size(), 6u);
  EXPECT_FALSE(arr.search(3));
  EXPECT_FALSE(arr.search(5));
  EXPECT_TRUE(arr.search(0));
  EXPECT_TRUE(arr.search(7));
  arr.insert(3);
  EXPECT_EQ(arr.size(), 7u);
  EXPECT_TRUE(arr.search(3));
}

TEST(BwArrayTest, LazyFixTriggered) {
  bwarray arr;
  insert_all(arr, {1, 2, 3, 4});
  arr.remove(1);
  arr.remove(2);
  EXPECT_EQ(arr.size(), 2u);
  EXPECT_FALSE(arr.search(1));
  EXPECT_FALSE(arr.search(2));
  EXPECT_TRUE(arr.search(3));
  EXPECT_TRUE(arr.search(4));
}

TEST(BwArrayTest, LazyFixLargeDataset) {
  bwarray arr;
  for (int i = 0; i < 2048; ++i) {
    arr.insert(i);
  }
  EXPECT_EQ(arr.size(), 2048u);
  for (int i = 0; i < 1024; ++i) {
    arr.remove(i);
  }
  EXPECT_EQ(arr.size(), 1024u);
  for (int i = 0; i < 1024; ++i) {
    EXPECT_FALSE(arr.search(i));
  }
  for (int i = 1024; i < 2048; ++i) {
    EXPECT_TRUE(arr.search(i));
  }
}

#include <map>
#include <random>

static void run_stress(int value_range, int ops, unsigned seed) {
  std::mt19937 rng(seed);
  bwarray arr;
  std::map<int, int> ref;

  for (int step = 0; step < ops; ++step) {
    int val = static_cast<int>(rng() % value_range);
    int op = static_cast<int>(rng() % 3);

    if (op == 0) {
      arr.insert(val);
      ref[val]++;
    } else if (op == 1) {
      arr.remove(val);
      if (ref.count(val) && ref[val] > 0) {
        if (--ref[val] == 0) {
          ref.erase(val);
        }
      }
    } else {
      bool expected = ref.count(val) && ref.at(val) > 0;
      ASSERT_EQ(arr.search(val), expected)
          << "search(" << val << ") mismatch at step " << step
          << " (seed=" << seed << ")";
    }

    if (op != 2) {
      std::size_t ref_sz = 0;
      for (auto& [k, cnt] : ref) {
        ref_sz += static_cast<std::size_t>(cnt);
      }
      ASSERT_EQ(arr.size(), ref_sz)
          << "size() mismatch at step " << step << " (seed=" << seed << ")";
    }
  }

  for (int v = 0; v < value_range; ++v) {
    bool expected = ref.count(v) && ref.at(v) > 0;
    ASSERT_EQ(arr.search(v), expected)
        << "final search(" << v << ") mismatch (seed=" << seed << ")";
  }
}

TEST(BwArrayStressTest, SmallRangeWithDuplicates) {
  for (unsigned seed = 0; seed < 20; ++seed) {
    run_stress(/*value_range=*/8, /*ops=*/500, seed);
  }
}

TEST(BwArrayStressTest, MediumRange) {
  for (unsigned seed = 0; seed < 10; ++seed) {
    run_stress(/*value_range=*/256, /*ops=*/1000, seed);
  }
}

TEST(BwArrayStressTest, LargeDataset) {
  run_stress(/*value_range=*/512, /*ops=*/5000, /*seed=*/42);
}
