#include <algorithm>
#include <climits>
#include <cmath>
#include <csignal>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::unordered_set;
using std::vector;

int binary_search(vector<int> &a, int el) {
  int res = -1;
  int l = 0, r = a.size() - 1;
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
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;

  const int CONSTRAINS = 1000000000;

  vector<int> triples;

  for (int n = 1; n * n + (n + 1) * (n + 1) <= CONSTRAINS; ++n) {
    for (int m = n + 1; n * n + m * m <= CONSTRAINS; ++m) {
      auto a = m * m - n * n;
      auto b = 2 * m * n;
      auto c = n * n + m * m;
      if (c == a * a - b) {
        triples.push_back(c);
      }
    }
  }

  sort(triples.begin(), triples.end());

  while (t--) {
    size_t n;
    std::cin >> n;

    auto res = binary_search(triples, n + 1);
    if (res == -1) {
      std::cout << triples.size() << "\n";
    } else {
      std::cout << res << "\n";
    }
  }

  return 0;
}
