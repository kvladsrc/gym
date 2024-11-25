#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int withdraw(int n, vector<int> coins) {
  int res = 0;
  int max = 0;
  while (n) {
    if (coins[max] <= n) {
      n -= coins[max];
      res++;
    } else {
      max++;
    }
  }
  return res;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> coins = {100, 20, 10, 5, 1};
  std::cout << withdraw(n, coins) << "\n";

  return 0;
}
