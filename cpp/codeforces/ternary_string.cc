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

int ternary_string_naive(const string& s, int start, int end) {
  int min_cur = s.size() + 1;
  for (int i = start; i <= end; ++i) {
    bool one = false, two = false, three = false;
    int window = 0;
    while ((!one || !two || !three) && i + window <= end) {
      char next = s[i + window];
      if (next == '1') one = true;
      if (next == '2') two = true;
      if (next == '3') three = true;
      window++;
    }

    if (one && two && three) {
      if (window < min_cur) min_cur = window;
    }
  }

  if (min_cur < static_cast<int>(s.size()) + 1) {
    return min_cur;
  }

  return 0;
}

int ternary_string_dc(const string& s, int start, int end) {
  if (start >= end) return 0;

  int mid = (start + end) / 2;
  int left_half = ternary_string_dc(s, start, mid);
  int right_half = ternary_string_dc(s, mid + 1, end);

  int best;
  if (!left_half || !right_half) {
    best = left_half + right_half;
  } else {
    best = min(left_half, right_half);
  }

  if (best == 0) {
    return ternary_string_naive(s, start, end);
  }

  int mid_best = ternary_string_naive(s, mid - best, mid + best);

  if (mid_best == 0) return best;

  return min(best, mid_best);
}

vector<int> sums(const string& s, char c) {
  vector<int> sum(s.size(), 0);
  if (s[0] == c) {
    sum[0] = 1;
  }

  for (int i = 1; i < static_cast<int>(s.size()); ++i) {
    sum[i] = sum[i - 1];
    if (s[i] == c) {
      sum[i]++;
    }
  }

  return sum;
}

int binary_search(const vector<int>& sums, int target) {
  if (target > sums.back()) return 0;

  int left = 0, right = sums.size() - 1;
  while (left <= right) {
    int mid = (left + right) / 2;
    if (sums[mid] >= target) {
      right = mid - 1;
    } else {
      left = mid + 1;
    }
  }

  if (sums[left] == target) {
    return left;
  }

  return 0;
}

int ternary_string(const string& s) {
  vector<int> ones = sums(s, '1');
  vector<int> twos = sums(s, '2');
  vector<int> threes = sums(s, '3');

  int inf = s.size() + 1;
  int best_result = inf;

  for (int i = 0; i < static_cast<int>(s.size()) - 1; ++i) {
    if (s[i] == s[i + 1]) continue;

    char c = s[i];
    int case1, case2;
    if (c == '1') {
      case1 = binary_search(twos, twos[i] + 1);
      case2 = binary_search(threes, threes[i] + 1);
    } else if (c == '2') {
      case1 = binary_search(ones, ones[i] + 1);
      case2 = binary_search(threes, threes[i] + 1);
    } else {
      case1 = binary_search(ones, ones[i] + 1);
      case2 = binary_search(twos, twos[i] + 1);
    }

    if (case1 && case2) {
      int result = max(case1, case2) - i + 1;
      if (result < best_result) {
        best_result = result;
      }
    }
  }

  return best_result == inf ? 0 : best_result;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;
    std::cout << ternary_string(s) << "\n";
  }

  return 0;
}
