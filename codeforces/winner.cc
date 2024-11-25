#include <algorithm>
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

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  unordered_map<string, vector<pair<int, int>>> score_timeseries;
  for (int round = 1; round <= n; ++round) {
    string name;
    std::cin >> name;
    int score;
    std::cin >> score;
    int prev_score =
        (score_timeseries[name].empty() ? 0
                                        : score_timeseries[name].back().first);
    score_timeseries[name].push_back({score + prev_score, round});
  }

  string winner;
  int winner_score = -1000000, winner_round = 0;
  for (auto player_metric : score_timeseries) {
    if (player_metric.second.back().first > winner_score) {
      winner_score = player_metric.second.back().first;
      winner = player_metric.first;
      for (auto p : player_metric.second) {
        if (p.first >= winner_score) {
          winner_round = p.second;
          break;
        }
      }

      continue;
    }

    if (player_metric.second.back().first == winner_score) {
      for (auto p : player_metric.second) {
        if (p.first >= winner_score && p.second < winner_round) {
          winner = player_metric.first;
          winner_round = p.second;
          break;
        }
      }
    }
  }

  std::cout << winner << "\n";

  return 0;
}
