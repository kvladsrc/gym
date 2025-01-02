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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while ((t--) != 0) {
    uint64_t n;
    std::cin >> n;

    // 1111
    // 1110
    // 1101
    // 1011
    // 0111

    // Len of the sequence is the number of ones in binary
    // representation of N.

    vector<uint64_t> res;
    res.push_back(n);
    uint64_t bm = 1;
    auto n_cpy = n;
    while (n_cpy != 0) {
      if (n_cpy % 2 == 1) {
        auto el = n ^ bm;
        if (el != 0) {
          res.push_back(el);
        }
      }

      bm *= 2;
      n_cpy /= 2;
    }

    std::reverse(res.begin(), res.end());
    std::cout << res.size() << "\n";
    for (auto el : res) {
      std::cout << el << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
