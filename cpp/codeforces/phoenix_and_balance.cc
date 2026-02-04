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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> coins;
    int power = 1, coin = 2;
    while (power <= n) {
      coins.push_back(coin);
      coin *= 2;
      power++;
    }

    int a_bucket = 0, b_bucket = 0;
    int start = 0, end = coins.size() - 1;
    bool a_turn = true;
    while (start <= end) {
      if (a_turn) {
        if (a_bucket <= b_bucket) {
          a_bucket += coins[end--];
        } else {
          a_bucket += coins[start++];
        }
        a_turn = false;
      } else {
        if (b_bucket <= a_bucket) {
          b_bucket += coins[end--];
        } else {
          b_bucket += coins[start++];
        }
        a_turn = true;
      }
    }

    std::cout << abs(a_bucket - b_bucket) << "\n";
  }
  return 0;
}
