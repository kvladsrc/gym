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

int find_worm(vector<int> &piles, int num) {
  if (num <= piles[0]) return 0;
  if (num > piles[piles.size() - 1]) return piles.size() - 1;

  int start = 0, end = piles.size() - 1, mid = end / 2;

  while (end - start > 1) {
    if (piles[mid] == num) return mid;

    if (piles[mid] > num) {
      end = mid;
    } else {
      start = mid;
    }

    mid = (start + end) / 2;
  }

  return end;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  int total = 0;
  vector<int> piles(n);
  for (int i = 0; i < n; ++i) {
    int num;
    std::cin >> num;
    total += num;
    piles[i] = total;
  }

  int m;
  std::cin >> m;
  while (m--) {
    int num;
    std::cin >> num;
    std::cout << find_worm(piles, num) + 1 << "\n";
  }
  return 0;
}
