#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int binary_count(vector<int> &a, int val) {
  // Not nessesary. l <= r solve the problem.
  if (val < a[0]) return a.size();
  if (a[a.size() - 1] <= val) return 0;

  int l = 0, r = a.size() - 1;
  int mid = l + ((r - l) / 2);  // mid can be define inside while block.

  while (l < r) {
    if (a[mid] <= val) {
      l = mid + 1;
    } else {
      r = mid;
    }

    mid = l + ((r - l) / 2);
  }

  int res = a.size() - r;
  return res;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  int64_t total = 0;
  vector<int> in(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> in[i];
    total += in[i];
  }

  if (total % 3) {
    std::cout << 0 << "\n";
    return 0;
  }

  vector<int> left_cuts;
  vector<int> right_cuts;
  int64_t left_sum = 0;

  for (int i = 0; i < n; ++i) {
    left_sum += in[i];

    if (left_sum == total / 3) {
      left_cuts.push_back(i);
    }

    if (total - left_sum == total / 3 && i + 1 < n) {
      right_cuts.push_back(i + 1);
    }
  }

  int64_t res = 0;
  for (int left_cut : left_cuts) {
    int tmp = binary_count(right_cuts, left_cut + 1);
    res += tmp;
  }

  std::cout << res << "\n";

  return 0;
}
