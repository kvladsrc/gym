#include <algorithm>
#include <vector>

#include <gtest/gtest.h>

using std::sort;
using std::vector;

int binary_search(vector<int> &a, int i) {
  int l = 0, r = a.size() - 1;
  int res = -1;

  while (l <= r) {
    int mid = (l + r) / 2;

    if (a[mid] > i) {
      r = mid - 1;
      res = mid;      
    } else {
      l = mid + 1;
    }
  }

  return res;
}

class BinSearchTest : public ::testing::Test {};

TEST_F(BinSearchTest, Check) {
  srand(time(NULL));
  const int tests = 100000;
  const int max_size = 200;

  for (int i = 0; i < tests; ++i) {
    int rand_size = (rand() % max_size) + 1;

    vector<int> array(rand_size);
    for (auto &i : array) {
      i = rand() % max_size;
    }
    sort(array.begin(), array.end());

    bool check = true;
    int el, pos;
    for (int j = 0; j < (tests / 10); ++j) {
      el = rand() % (max_size + 100);
      pos = binary_search(array, el);

      if (pos == -1 && array.back() >= el) {
        check = false;
        break;
      }

      if (pos != -1 && array[pos] < el) {
        check = false;
        break;
      }

      if (pos > 0 && array[pos - 1] >= el) {
        check = false;
        break;
      }
    }

    if (!check) {
      for (auto &i : array) {
        std::cout << i << " ";
      }
      std::cout << std::endl;
      std::cout << "Element: " << el << std::endl;
      std::cout << "Position: " << pos << std::endl;
    }

    ASSERT_TRUE(check);
  }
}
