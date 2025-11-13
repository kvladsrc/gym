#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int64_t sum_of_seq(int64_t n) { return ((n + 1) * n) / 2; }

int bin_search(int l, int r, int64_t k) {
  if (sum_of_seq(r) + 1 <= k) return r;

  while (r - l > 1) {
    int mid = (r + l) / 2;
    if (sum_of_seq(mid) + 1 <= k) {
      l = mid;
    } else {
      r = mid;
    }
  }

  return l;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t n, k;
    std::cin >> n >> k;

    int first_shift = bin_search(0, n - 2, k);
    int second_shift = k - (sum_of_seq(first_shift) + 1);
    first_shift++;

    string out(n, 'a');
    out[first_shift] = out[second_shift] = 'b';
    std::reverse(out.begin(), out.end());
    std::cout << out << "\n";
  }

  return 0;
}
