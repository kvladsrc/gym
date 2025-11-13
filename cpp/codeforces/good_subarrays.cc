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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t n;
    std::cin >> n;

    vector<int> prefix_sums(n + 1);
    prefix_sums[0] = 0;
    unordered_map<int, int64_t> m;
    m[0]++;

    for (int i = 1; i <= n; ++i) {
      char buf;
      std::cin >> buf;
      prefix_sums[i] = prefix_sums[i - 1] + (buf - '0');
      m[prefix_sums[i] - i]++;
    }

    int64_t res = std::accumulate(
        m.begin(), m.end(), int64_t{0}, [](int64_t acc, const auto& p) {
          return acc + (p.second * (p.second - 1)) / 2;
        });

    std::cout << res << "\n";
  }

  return 0;
}
