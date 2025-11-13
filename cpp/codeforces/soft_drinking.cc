#include <algorithm>
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

int main(int argc, char *argv[]) {
  // This winter is so cold in Nvodsk! A group of n friends decided to
  // buy k bottles of a soft drink called "Take-It-Light" to warm up a
  // bit. Each bottle has l milliliters of the drink. Also they bought
  // c limes and cut each of them into d slices. After that they found
  // p grams of salt.

  // To make a toast, each friend needs nl milliliters of the drink, a
  // slice of lime and np grams of salt. The friends want to make as
  // many toasts as they can, provided they all drink the same
  // amount. How many toasts can each friend make?
  int n, k, l, c, d, p, nl, np;
  std::cin >> n >> k >> l >> c >> d >> p >> nl >> np;
  int slices = c * d;
  int soft_drink = k * l;
  vector<int> toasts;
  toasts.push_back(soft_drink / nl);
  toasts.push_back(slices);
  toasts.push_back(p / np);
  int min = toasts[0];
  for (int t : toasts) {
    if (min > t) min = t;
  }
  std::cout << min / n << "\n";

  return 0;
}
