#include <algorithm>
#include <climits>
#include <cmath>
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
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    int k = 0;
    std::cin >> n >> k;

    vector<int64_t> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    // Idea is to represent each a_i by i_1, i_2, ..., i_n where a_i =
    // k^i_1 + k^i_2 + ... + k^i_n. Then we can find the answer if all
    // i are differ.

    map<int, int> m;
    for (auto el : a) {
      int i = 0;
      while (el > 0) {
        if (el % k == 0) {
          el /= k;
          i++;
          continue;
        }

        el--;
        m[i]++;
      }
    }

    bool res = true;
    for (auto p : m) {
      if (p.second > 1) {
        res = false;
        break;
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
