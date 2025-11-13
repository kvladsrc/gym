#include <algorithm>
#include <cstdint>
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

struct node {
  int64_t sum;
  node *left;
  node *right;
};

node *build_sums(const vector<int64_t> &nums, int start, int end) {
  auto res = new node;
  if (start == end) {
    res->sum = nums[start];
    return res;
  }

  int mid = (start + end) / 2;
  res->left = build_sums(nums, start, mid);
  res->right = build_sums(nums, mid + 1, end);
  res->sum = res->left->sum + res->right->sum;

  return res;
}

int64_t query(node *root, int l, int r, int cur_l, int cur_r) {
  if (l <= cur_l && cur_r <= r) {
    return root->sum;
  }

  if (cur_r < l || r < cur_l) {
    return 0;
  }

  int mid = (cur_l + cur_r) / 2;
  int64_t left = query(root->left, l, r, cur_l, mid);
  int64_t right = query(root->right, l, r, mid + 1, cur_r);

  return left + right;
}

void simple_solution() {
  int n;
  std::cin >> n;
  vector<int64_t> a(n), pref_sum_a(n + 1);
  vector<int64_t> sorted_a(n), pref_sum_sorted_a(n + 1);
  pref_sum_a.front() = 0;
  pref_sum_sorted_a.front() = 0;

  for (size_t idx = 0; idx < static_cast<size_t>(n); ++idx) {
    std::cin >> a[idx];
    sorted_a[idx] = a[idx];
  }

  sort(sorted_a.begin(), sorted_a.end());
  for (size_t idx = 0; idx < static_cast<size_t>(n); ++idx) {
    pref_sum_a[idx + 1] = pref_sum_a[idx] + a[idx];
    pref_sum_sorted_a[idx + 1] = pref_sum_sorted_a[idx] + sorted_a[idx];
  }

  int q;
  std::cin >> q;
  while (q--) {
    int t, l, r;
    std::cin >> t >> l >> r;
    if (t == 1) {
      std::cout << pref_sum_a[r] - pref_sum_a[l - 1] << "\n";
    } else {
      std::cout << pref_sum_sorted_a[r] - pref_sum_sorted_a[l - 1] << "\n";
    }
  }
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  vector<int64_t> nums(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> nums[i];
  }

  auto sum_tree_orig = build_sums(nums, 0, n - 1);
  sort(nums.begin(), nums.end());
  auto sum_tree_sort = build_sums(nums, 0, n - 1);

  int m;
  std::cin >> m;
  while (m--) {
    int l, r, t;
    std::cin >> t >> l >> r;
    l--;
    r--;
    if (t == 1) {
      std::cout << query(sum_tree_orig, l, r, 0, n - 1) << "\n";
    } else {
      std::cout << query(sum_tree_sort, l, r, 0, n - 1) << "\n";
    }
  }

  return 0;
}
