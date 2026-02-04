#include <algorithm>
#include <climits>
#include <cmath>
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
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;

  while (t--) {
    int64_t n;
    std::cin >> n;

    map<int64_t, int> divisors;
    for (int64_t i = 2; i * i <= n; ++i) {
      if (n % i == 0) {
        int local_max = 0;
        auto n_cpy = n;
        while (n_cpy % i == 0) {
          local_max++;
          n_cpy /= i;
        }
        divisors[i] = local_max;
      }
    }

    int longest_cnt = 1;
    int64_t longest_divisor = n;
    for (auto p : divisors) {
      if (p.second > longest_cnt) {
        longest_cnt = p.second;
        longest_divisor = p.first;
      }
    }

    if (longest_cnt == 1) {
      std::cout << 1 << "\n";
      std::cout << n << "\n";
    } else {
      std::cout << longest_cnt << "\n";
      for (int i = 1; i < longest_cnt; ++i) {
        std::cout << longest_divisor << " ";
        n /= longest_divisor;
      }
      std::cout << n << "\n";
    }
  }

  return 0;
}
