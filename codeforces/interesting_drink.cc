#include <algorithm>
#include <cstddef>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int bin_search(vector<int> shops, int i) {
  if (i < shops[0])
    return 0;
  if (i >= shops[shops.size() - 1])
    return shops.size();

  int left = 0, right = shops.size() - 1, mid = right / 2;

  while (left + 1 < right) {
    if (i >= shops[mid]) {
      left = mid;
    } else {
      right = mid;
    }

    mid = (left + right) / 2;
  }

  return left + 1;
}

#define MAX_PRICE 100000
vector<int> prefix_sum(vector<int> &shops) {
  vector<int> dp(MAX_PRICE + 1);
  size_t ptr = 0;
  for (int i = 1; i <= MAX_PRICE; ++i) {
    dp[i] = dp[i - 1];

    while (i >= shops[ptr] && ptr < shops.size()) {
      dp[i]++;
      ptr++;
    }
  }

  return dp;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> shops(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> shops[i];
  }

  sort(shops.begin(), shops.end());
  auto dp = prefix_sum(shops);

  int q;
  std::cin >> q;
  while (q) {
    int available;
    std::cin >> available;
    if (available > MAX_PRICE) {
      std::cout << shops.size() << "\n";
    } else {
      std::cout << dp[available] << "\n";
    }
    q--;
  }

  return 0;
}
