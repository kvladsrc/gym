#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int binary_search_first(vector<int64_t> &a, int64_t i) {
  int l = 0, r = a.size() - 1;
  size_t res = -1;
  while (l <= r) {
    int mid = (l + r) / 2;
    if (a[mid] >= i) {
      r = mid - 1;
      res = mid;
    } else {
      l = mid + 1;
    }
  }

  return res;
}

int64_t count_pairs(vector<int64_t> &a, vector<int64_t> &b) {
  int64_t res = 0;
  vector<int64_t> diff(a.size());
  for (size_t i = 0; i < a.size(); ++i) {
    diff[i] = b[i] - a[i];
  }

  sort(diff.begin(), diff.end());
  for (size_t i = 0; i < diff.size(); ++i) {
    int pos = binary_search_first(diff, (diff[i] * -1));
    if (pos != -1) {
      res += pos;
      if (pos > static_cast<int>(i)) {
        res--;
      }
    } else {
      res += diff.size() - 1;
    }
  }
  res /= 2;

  return res;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int64_t> a(n), b(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> a[i];
  }
  for (int i = 0; i < n; ++i) {
    std::cin >> b[i];
  }

  std::cout << count_pairs(a, b) << std::endl;

  return 0;
}
