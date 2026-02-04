#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int binary_search(const vector<int>& sums, int t, int start, int end) {
  int mid = (start + end) / 2, i = start, j = end;
  int before = start == 0 ? 0 : sums[start - 1];

  if (sums[start] - before > t) return 0;
  if (sums[end] - before <= t) return end - start + 1;

  while (j - i > 1) {
    if (sums[mid] - before > t) {
      j = mid;
    } else {
      i = mid;
    }
    mid = (i + j) / 2;
  }

  int res = j - start;
  return res;
}

int best_seq_naive(const vector<int>& sums, int t, int start, int end) {
  int best_seq = 0;
  for (int i = start; i <= end; ++i) {
    int best_from_i = binary_search(sums, t, i, end);
    if (best_from_i > best_seq) best_seq = best_from_i;
    if (i + best_seq >= end) break;
  }
  return best_seq;
}

int best_seq(vector<int>& time_to_read, int t, int start, int end) {
  if (start >= end - t) {
    return time_to_read[start] <= t ? 1 : 0;
  }

  int mid = (start + end) / 2;
  int l_best = best_seq(time_to_read, t, start, mid);
  int r_best = best_seq(time_to_read, t, mid + 1, end);
  int m_best = best_seq_naive(time_to_read, t, mid + 1 - l_best, mid + r_best);

  return max(max(l_best, r_best), m_best);
}

int main(int argc, char* argv[]) {
  int n, t;
  std::cin >> n >> t;
  vector<int> time_to_read(n);
  vector<int> sums(n);
  int sum = 0;
  for (int i = 0; i < n; ++i) {
    std::cin >> time_to_read[i];
    sum += time_to_read[i];
    sums[i] = sum;
  }
  std::cout << best_seq_naive(sums, t, 0, n - 1) << "\n";

  return 0;
}
