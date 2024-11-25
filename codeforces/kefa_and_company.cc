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

bool compare(const pair<int, int> &a, const pair<int, int> &b) {
  return a.first < b.first;
}

int binary_search(const vector<pair<int, int>> &friends, int i) {
  int l = 0, r = friends.size() - 1;
  int pos = -1;

  while (l <= r) {
    int mid = l + (r - l) / 2;

    if (friends[mid].first < i) {
      l = mid + 1;
    } else {
      pos = mid;
      r = mid - 1;
    }
  }

  return pos;
}

int main(int argc, char *argv[]) {
  int n, d;
  std::cin >> n >> d;

  vector<pair<int, int>> friends(n);
  for (auto &p : friends) {
    std::cin >> p.first >> p.second;
  }

  sort(friends.begin(), friends.end(), compare);
  vector<int64_t> prefix_sum(n);
  prefix_sum[0] = friends[0].second;
  for (int i = 1; i < n; ++i) {
    prefix_sum[i] = prefix_sum[i - 1] + friends[i].second;
  }

  int64_t max_friendship = 0;
  for (int i = 0; i < n; ++i) {
    int min_money_threshold = friends[i].first - d + 1;
    int min_pos = binary_search(friends, min_money_threshold);

    int64_t friendship = prefix_sum[i];
    if (min_pos > 0) {
      friendship -= prefix_sum[min_pos - 1];
    }

    if (friendship > max_friendship) {
      max_friendship = friendship;
    }
  }

  std::cout << max_friendship << "\n";

  return 0;
}
