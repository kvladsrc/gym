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

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    string in;
    std::cin >> in;

    /*
     * Implementation:
     *
     * 0. If we have 0 on first or last posion then or odd number of
     * zeroes then the answer is NO (only one of two sequences can be
     * balanced).
     *
     * 1. First string can be produced by algorithm: lets construct
     * the sequence type of ()()...() using just a positions with
     * zeroes. Then balance the sequence by additional bracets.
     *
     * 2. Second string can be produced from the first.
     */

    if (n % 2 != 0) {
      std::cout << "NO" << "\n";
      continue;
    }

    if (in.front() == '0' || in.back() == '0') {
      std::cout << "NO" << "\n";
      continue;
    }

    int zeroes =
        std::count_if(in.begin(), in.end(), [](char c) { return c == '0'; });

    if (zeroes % 2 != 0) {
      std::cout << "NO" << "\n";
      continue;
    }

    std::cout << "YES" << "\n";

    string first(in.size(), '_');
    bool last_open = false;
    for (size_t idx = 0; idx < in.size(); ++idx) {
      if (in[idx] == '0') {
        if (last_open) {
          first[idx] = ')';
        } else {
          first[idx] = '(';
        }
        last_open = !last_open;
      }
    }

    size_t l = 0;
    size_t r = n - 1;
    while (true) {
      while (l < r && first[l] != '_') {
        l++;
      }

      while (l < r && first[r] != '_') {
        r--;
      }

      if (l >= r) {
        break;
      }

      first[l] = '(';
      first[r] = ')';
    }

    std::cout << first << "\n";

    for (size_t idx = 0; idx < first.size(); ++idx) {
      if (in[idx] == '1') {
        std::cout << first[idx];
      } else {
        if (first[idx] == '(') {
          std::cout << ')';
        } else {
          std::cout << '(';
        }
      }
    }
    std::cout << "\n";
  }

  return 0;
}
