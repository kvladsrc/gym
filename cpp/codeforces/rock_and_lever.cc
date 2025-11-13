#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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

int binary_search_with_duplicates(vector<int> &a, int el) {
  int l = 0, r = a.size() - 1;
  int res = -1;
  while (l <= r) {
    int mid = (l + r) / 2;
    if (a[mid] >= el) {
      res = mid;
      r = mid - 1;
    } else {
      l = mid + 1;
    }
  }
  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    sort(a.begin(), a.end());

    int64_t res = 0;
    for (int i = 0; i < (n - 1); ++i) {
      int power = 1;
      while (power <= a[i]) power = power << 1;
      int pos_right = binary_search_with_duplicates(a, power);
      if (pos_right == -1) {
        res += (n - 1) - i;
      } else {
        res += pos_right - (i + 1);
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
